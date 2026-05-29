using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using HyperPet.Core.Diagnostics;
using HyperPet.Core.Notifications;

namespace HyperPet.App.Notifications;

/// <summary>
/// Polls top-level popup windows of configured processes (like Zalo) that
/// surface notifications inside their own window instead of routing them
/// through the Windows Action Center. New small/topmost/no-taskbar windows
/// are treated as in-app notifications; their text is extracted via UI
/// Automation and surfaced as a <see cref="HyperNotification"/>.
/// </summary>
public sealed class InAppNotificationWatcher : IDisposable
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x00000008;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    private const int MinPopupWidth = 180;
    private const int MinPopupHeight = 48;
    private const int MaxPopupWidth = 700;
    private const int MaxPopupHeight = 260;
    private const int MaxTextNodes = 64;

    // Tooltip / popup-menu / fly-out window classes that masquerade as small
    // topmost popups but are not actual notifications.
    private static readonly string[] TooltipClassExactDenylist = new[]
    {
        "tooltips_class32",
        "#32768",                 // Win32 popup menu
        "QTip",                   // legacy Qt tooltip
        "MSO_BORDEREFFECT_WINDOW",
    };
    private static readonly string[] TooltipClassSubstringDenylist = new[]
    {
        "tooltip",                // generic
        "qtooltip",
        "popuphost",              // WinUI/WPF Popup
        "hwndwrapper[popup",      // WPF popup
    };

    private readonly DispatcherTimer _timer;
    private readonly HyperPetLogger? _logger;
    private readonly ChromiumAccessibilityActivator _activator;
    // hwnd -> last emitted content hash. Real Zalo / Electron popups often
    // reuse the same HWND for successive messages (text swapped in-place);
    // keying off handle alone would suppress every message after the first.
    private readonly Dictionary<IntPtr, string> _seenHandles = new();
    // hwnd -> last window rect + extracted text. Skips the cross-process UI
    // Automation walk when a popup hasn't moved/resized since last scan.
    private readonly Dictionary<IntPtr, (RECT Rect, string Title, string Body)> _extractCache = new();
    private readonly HashSet<IntPtr> _excludedHandles = new();
    private readonly string _selfProcessName = Process.GetCurrentProcess().ProcessName;
    private IReadOnlyList<string> _watchedProcessNames = Array.Empty<string>();

    public event EventHandler<HyperNotification>? Detected;

    public InAppNotificationWatcher(HyperPetLogger? logger = null)
    {
        _logger = logger;
        _activator = new ChromiumAccessibilityActivator(logger);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (_, _) => Scan();
    }

    /// <summary>
    /// Marks an HWND to skip during scans (e.g., HyperPet's own MainWindow so
    /// the watcher does not treat the pet window as an in-app notification).
    /// </summary>
    public void ExcludeHandle(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            _excludedHandles.Add(hwnd);
        }
    }

    public void SetInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromSeconds(1))
        {
            interval = TimeSpan.FromSeconds(1);
        }

        _timer.Interval = interval;
    }

    public void SetWatchList(IEnumerable<string> processNames)
    {
        _watchedProcessNames = processNames
            ?.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
    }

    public void Start()
    {
        if (_watchedProcessNames.Count == 0)
        {
            return;
        }

        // Pre-activate now so the first scan sees a populated Chromium
        // accessibility tree instead of host-element junk. Discard the
        // result; the side effect (UIA subscription) is what we want.
        _ = ResolveWatchedPids();

        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        Stop();
        _activator.Dispose();
        _seenHandles.Clear();
        _extractCache.Clear();
    }

    private void Scan()
    {
        if (_watchedProcessNames.Count == 0)
        {
            _timer.Stop();
            return;
        }

        var watchedPids = ResolveWatchedPids();
        if (watchedPids.Count == 0)
        {
            // Drop stale handle cache so reopens of the app get re-detected.
            _seenHandles.Clear();
            return;
        }

        var liveHandles = new HashSet<IntPtr>();

        try
        {
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                {
                    return true;
                }

                if (_excludedHandles.Contains(hwnd))
                {
                    return true;
                }

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (!watchedPids.TryGetValue((int)pid, out string? processName))
                {
                    return true;
                }

                if (!IsPopupCandidate(hwnd))
                {
                    return true;
                }

                // WPF/Win32 context menus, tooltips and popups created by
                // HyperPet itself report their owner as MainWindow. If the
                // root owner is excluded, skip the child popup too.
                IntPtr rootOwner = GetAncestor(hwnd, GA_ROOTOWNER);
                if (rootOwner != IntPtr.Zero && _excludedHandles.Contains(rootOwner))
                {
                    return true;
                }

                liveHandles.Add(hwnd);

                // Resolve display name first so ExtractText can filter the
                // app name out as junk.
                bool isSelfProc = string.Equals(processName, _selfProcessName, StringComparison.OrdinalIgnoreCase);
                string appName = isSelfProc ? "Sim" : processName;

                GetWindowRect(hwnd, out RECT rect);
                string title, body;
                if (_extractCache.TryGetValue(hwnd, out var cached) && RectEquals(cached.Rect, rect))
                {
                    title = cached.Title;
                    body = cached.Body;
                }
                else
                {
                    (title, body) = ExtractText(hwnd, appName);
                    _extractCache[hwnd] = (rect, title, body);
                }

                string contentHash = HashText(title + "" + body);

                if (_seenHandles.TryGetValue(hwnd, out string? lastHash) && lastHash == contentHash)
                {
                    return true;
                }

                _seenHandles[hwnd] = contentHash;
                EmitFor(hwnd, processName, appName, title, body);
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception exception)
        {
            _logger?.Warn("InAppNotificationWatcher EnumWindows failed", exception);
        }

        // Drop handles that no longer exist or moved out of the popup filter.
        var stale = _seenHandles.Keys.Where(h => !liveHandles.Contains(h)).ToList();
        foreach (var h in stale)
        {
            _seenHandles.Remove(h);
        }

        var staleExtract = _extractCache.Keys.Where(h => !liveHandles.Contains(h)).ToList();
        foreach (var h in staleExtract)
        {
            _extractCache.Remove(h);
        }
    }

    private Dictionary<int, string> ResolveWatchedPids()
    {
        var result = new Dictionary<int, string>();

        foreach (var name in _watchedProcessNames)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        result[process.Id] = name;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception exception)
            {
                _logger?.Warn($"InAppNotificationWatcher: lookup failed for '{name}'", exception);
            }
        }

        // Wake Chromium UIA tree on each watched process, and drop entries
        // whose pid is no longer alive (Zalo restart, etc.).
        foreach (var pid in result.Keys)
        {
            _activator.EnsureActivated(pid);
        }

        _activator.PruneStale(result.Keys);

        return result;
    }

    private static bool IsPopupCandidate(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out RECT rect))
        {
            return false;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width < MinPopupWidth || height < MinPopupHeight)
        {
            return false;
        }

        if (width > MaxPopupWidth || height > MaxPopupHeight)
        {
            return false;
        }

        string className = GetWindowClassNameSafe(hwnd);
        if (IsTooltipLikeClass(className))
        {
            return false;
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

        // Click-through windows (tooltips, hover hints) carry WS_EX_TRANSPARENT.
        // Real notifications need pointer input to dismiss / click.
        if ((exStyle & WS_EX_TRANSPARENT) != 0)
        {
            return false;
        }

        bool hasAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;
        bool hasToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;

        // Skip main app windows that show in the taskbar (Zalo's primary
        // window). A tool window is fine (no taskbar entry).
        if (hasAppWindow && !hasToolWindow)
        {
            return false;
        }

        // Real Zalo notifications are not strictly WS_EX_TOPMOST; some are
        // raised via SetWindowPos with HWND_TOPMOST without the style bit
        // sticking. Accept either topmost OR no-activate (typical popup hint).
        bool isTopmost = (exStyle & WS_EX_TOPMOST) != 0;
        bool isNoActivate = (exStyle & WS_EX_NOACTIVATE) != 0;
        return isTopmost || isNoActivate || hasToolWindow;
    }

    private static bool IsTooltipLikeClass(string className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return false;
        }

        foreach (var exact in TooltipClassExactDenylist)
        {
            if (string.Equals(className, exact, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var sub in TooltipClassSubstringDenylist)
        {
            if (className.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetWindowClassNameSafe(IntPtr hwnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        int len = GetClassName(hwnd, buffer, buffer.Capacity);
        return len > 0 ? buffer.ToString() : string.Empty;
    }

    private void EmitFor(IntPtr hwnd, string processName, string appName, string title, string body)
    {

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            title = appName;
            body = "New message";
        }
        else if (string.IsNullOrWhiteSpace(title))
        {
            title = appName;
        }

        string sourceId = $"inapp:{appName}:{hwnd.ToInt64():X}:{HashText(title + body)}";

        var notification = new HyperNotification(
            sourceId: sourceId,
            appName: appName,
            title: title,
            body: body,
            timestamp: DateTimeOffset.UtcNow,
            canActivate: false,
            appUserModelId: appName);

        _logger?.Info($"InApp popup detected proc='{processName}' as='{appName}' title='{title}'");
        Detected?.Invoke(this, notification);
    }

    // Names returned by UI Automation for HWND-wrapper / platform host
    // elements that show up before the actual user-visible text inside
    // Chromium / Electron / WPF popups. These would otherwise be picked
    // as title / body.
    private static readonly HashSet<string> JunkAutomationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Chrome Legacy Window",
        "Intermediate D3D Window",
        "MSCTFIME UI",
        "Default IME",
        "IME",
        "GDI+ Window",
        "CicMarshalWnd",
    };

    private static (string title, string body) ExtractText(IntPtr hwnd, string appName)
    {
        try
        {
            AutomationElement? root = AutomationElement.FromHandle(hwnd);
            if (root is null)
            {
                return (string.Empty, string.Empty);
            }

            var texts = new List<string>();
            CollectNames(root, TreeWalker.ControlViewWalker, texts);

            // Strip platform host-element junk and the app name itself
            // (e.g. "Zalo") so the first remaining names are the actual
            // sender / message content the user wants to read.
            var deduped = texts
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => !JunkAutomationNames.Contains(t))
                .Where(t => !string.Equals(t, appName, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToList();

            string title = deduped.ElementAtOrDefault(0) ?? string.Empty;
            string body = deduped.ElementAtOrDefault(1) ?? string.Empty;
            return (title, body);
        }
        catch (ElementNotAvailableException)
        {
            return (string.Empty, string.Empty);
        }
        catch (Exception)
        {
            return (string.Empty, string.Empty);
        }
    }

    private static void CollectNames(AutomationElement element, TreeWalker walker, List<string> sink)
    {
        if (sink.Count >= MaxTextNodes)
        {
            return;
        }

        try
        {
            string name = element.Current.Name;
            if (!string.IsNullOrEmpty(name))
            {
                sink.Add(name);
            }
        }
        catch
        {
        }

        AutomationElement? child = null;
        try
        {
            child = walker.GetFirstChild(element);
        }
        catch
        {
            return;
        }

        while (child is not null)
        {
            CollectNames(child, walker, sink);
            try
            {
                child = walker.GetNextSibling(child);
            }
            catch
            {
                return;
            }
        }
    }

    private static string HashText(string text)
    {
        int hash = 17;
        foreach (char c in text)
        {
            hash = hash * 31 + c;
        }

        return hash.ToString("X");
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private const uint GA_ROOTOWNER = 3;

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private static bool RectEquals(RECT a, RECT b)
        => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
