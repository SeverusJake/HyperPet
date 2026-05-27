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
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;

    private const int MaxPopupWidth = 600;
    private const int MaxPopupHeight = 320;
    private const int MaxTextNodes = 64;

    private readonly DispatcherTimer _timer;
    private readonly HyperPetLogger? _logger;
    private readonly HashSet<IntPtr> _seenHandles = new();
    private readonly HashSet<IntPtr> _excludedHandles = new();
    private readonly string _selfProcessName = Process.GetCurrentProcess().ProcessName;
    private IReadOnlyList<string> _watchedProcessNames = Array.Empty<string>();

    public event EventHandler<HyperNotification>? Detected;

    public InAppNotificationWatcher(HyperPetLogger? logger = null)
    {
        _logger = logger;
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

        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        Stop();
        _seenHandles.Clear();
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

                liveHandles.Add(hwnd);

                if (_seenHandles.Contains(hwnd))
                {
                    return true;
                }

                _seenHandles.Add(hwnd);
                EmitFor(hwnd, processName);
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception exception)
        {
            _logger?.Warn("InAppNotificationWatcher EnumWindows failed", exception);
        }

        // Drop handles that no longer exist or moved out of the popup filter.
        _seenHandles.RemoveWhere(h => !liveHandles.Contains(h));
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
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (width > MaxPopupWidth || height > MaxPopupHeight)
        {
            return false;
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        bool hasAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;
        bool hasToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
        bool isTopmost = (exStyle & WS_EX_TOPMOST) != 0;

        if (hasAppWindow && !hasToolWindow)
        {
            return false;
        }

        return isTopmost;
    }

    private void EmitFor(IntPtr hwnd, string processName)
    {
        (string title, string body) = ExtractText(hwnd);

        // Popups from HyperPet itself (the debug press-9 simulator) get a
        // friendlier AppName so the messaging-rule auto-discovery does not
        // pollute the user's list with the exe name.
        bool isSelf = string.Equals(processName, _selfProcessName, StringComparison.OrdinalIgnoreCase);
        string appName = isSelf ? "Sim" : processName;

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

    private static (string title, string body) ExtractText(IntPtr hwnd)
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

            var deduped = texts
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
