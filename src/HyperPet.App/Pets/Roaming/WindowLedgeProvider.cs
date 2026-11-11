using System.Runtime.InteropServices;
using System.Windows;
using HyperPet.App.Views;

namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// Supplies perch ledges from live Win32 state: visible top-level captioned
/// windows and synthetic desktop edges, normalized to the pet's monitor and DPI
/// when a work-area provider is supplied.
/// </summary>
public sealed class WindowLedgeProvider : ILedgeProvider
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const int DWMWA_CLOAKED = 14;
    private const int MinLedgeWidth = 80;

    private readonly IntPtr _selfHwnd;
    private readonly Func<DateTime> _clock;
    private readonly Func<WorkArea>? _workArea;
    private readonly Func<(double X, double Y)>? _dpiScale;
    private (WorkArea Area, double X, double Y, double PetHeight)? _cacheContext;
    private IReadOnlyList<Ledge>? _cache;
    private DateTime _cacheUtc;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMilliseconds(1000);

    public WindowLedgeProvider(IntPtr selfHwnd) : this(selfHwnd, () => DateTime.UtcNow)
    {
    }

    public WindowLedgeProvider(IntPtr selfHwnd, Func<WorkArea> workArea, Func<(double X, double Y)> dpiScale)
        : this(selfHwnd, () => DateTime.UtcNow)
    {
        _workArea = workArea ?? throw new ArgumentNullException(nameof(workArea));
        _dpiScale = dpiScale ?? throw new ArgumentNullException(nameof(dpiScale));
    }

    /// <summary>Required space above a window ledge, in the pet window's DIPs.</summary>
    public double PetHeight { get; set; }

    internal WindowLedgeProvider(IntPtr selfHwnd, Func<DateTime> clock)
    {
        _selfHwnd = selfHwnd;
        _clock = clock;
    }

    internal static bool ShouldRebuild(DateTime nowUtc, DateTime cacheUtc, bool hasCache, TimeSpan ttl)
        => !hasCache || (nowUtc - cacheUtc) >= ttl;

    public IReadOnlyList<Ledge> GetLedges()
    {
        DateTime now = _clock();
        var context = GetContext();
        if (context == _cacheContext && !ShouldRebuild(now, _cacheUtc, _cache is not null, CacheTtl))
        {
            return _cache!;
        }

        var ledges = new List<Ledge>();

        EnumWindows((hwnd, _) =>
        {
            if (Qualifies(hwnd) && GetWindowRect(hwnd, out RECT r))
            {
                var ledge = Normalize(new Ledge(hwnd, r.Left, r.Right, r.Top), context);
                if (ledge is not null)
                {
                    ledges.Add(ledge);
                }
            }

            return true;
        }, IntPtr.Zero);

        if (context is { } selected)
        {
            ledges.Add(new Ledge(null, selected.Area.Left, selected.Area.Right, selected.Area.Bottom));
        }
        else
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero && GetWindowRect(tray, out RECT t))
            {
                ledges.Add(new Ledge(null, t.Left, t.Right, t.Top));
            }

            Rect wa = SystemParameters.WorkArea;
            ledges.Add(new Ledge(null, wa.Left, wa.Right, wa.Top));
        }

        _cache = ledges;
        _cacheUtc = now;
        _cacheContext = context;
        return ledges;
    }

    public Ledge? TryRefresh(Ledge ledge)
    {
        if (ledge.Hwnd is not IntPtr hwnd)
        {
            if (GetContext() is { } selected)
            {
                return new Ledge(null, selected.Area.Left, selected.Area.Right, selected.Area.Bottom);
            }

            return ledge;
        }

        if (!Qualifies(hwnd) || !GetWindowRect(hwnd, out RECT r))
        {
            _cache = null;
            return null;
        }

        var refreshed = Normalize(new Ledge(hwnd, r.Left, r.Right, r.Top), GetContext());
        if (refreshed is null)
        {
            _cache = null;
        }

        return refreshed;
    }

    private (WorkArea Area, double X, double Y, double PetHeight)? GetContext()
    {
        if (_workArea is null || _dpiScale is null)
        {
            return null;
        }

        var scale = _dpiScale();
        DesktopMonitors.ValidateScale(scale.X, nameof(scale.X));
        DesktopMonitors.ValidateScale(scale.Y, nameof(scale.Y));
        return (_workArea(), scale.X, scale.Y, PetHeight);
    }

    private static Ledge? Normalize(Ledge ledge, (WorkArea Area, double X, double Y, double PetHeight)? context)
        => context is { } selected
            ? ToWorkAreaLedge(ledge, selected.Area, selected.X, selected.Y, selected.PetHeight)
            : ledge.Width >= MinLedgeWidth ? ledge : null;

    internal static Ledge? ToWorkAreaLedge(Ledge physicalLedge, WorkArea workArea,
        double scaleX, double scaleY, double petHeight = 0)
    {
        DesktopMonitors.ValidateScale(scaleX, nameof(scaleX));
        DesktopMonitors.ValidateScale(scaleY, nameof(scaleY));
        double left = Math.Max(physicalLedge.Left / scaleX, workArea.Left);
        double right = Math.Min(physicalLedge.Right / scaleX, workArea.Right);
        double top = physicalLedge.TopY / scaleY;
        if (right - left < MinLedgeWidth || top < workArea.Top + Math.Max(0, petHeight) || top > workArea.Bottom)
        {
            return null;
        }

        return new Ledge(physicalLedge.Hwnd, left, right, top);
    }

    private bool Qualifies(IntPtr hwnd)
    {
        if (hwnd == _selfHwnd || hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        if ((style & WS_CAPTION) != WS_CAPTION)
        {
            return false;
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if (IsCloaked(hwnd))
        {
            return false;
        }

        return true;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            int cloaked = 0;
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int));
            return hr == 0 && cloaked != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
