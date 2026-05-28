using System.Runtime.InteropServices;
using System.Windows;

namespace HyperPet.App.Pets.Roaming;

/// <summary>
/// Supplies perch ledges from live Win32 state: visible top-level captioned
/// windows, the taskbar top edge, and the primary work-area top.
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

    public WindowLedgeProvider(IntPtr selfHwnd)
    {
        _selfHwnd = selfHwnd;
    }

    public IReadOnlyList<Ledge> GetLedges()
    {
        var ledges = new List<Ledge>();

        EnumWindows((hwnd, _) =>
        {
            if (Qualifies(hwnd) && GetWindowRect(hwnd, out RECT r))
            {
                double width = r.Right - r.Left;
                if (width >= MinLedgeWidth)
                {
                    ledges.Add(new Ledge(hwnd, r.Left, r.Right, r.Top));
                }
            }

            return true;
        }, IntPtr.Zero);

        IntPtr tray = FindWindow("Shell_TrayWnd", null);
        if (tray != IntPtr.Zero && GetWindowRect(tray, out RECT t))
        {
            ledges.Add(new Ledge(null, t.Left, t.Right, t.Top));
        }

        Rect wa = SystemParameters.WorkArea;
        ledges.Add(new Ledge(null, wa.Left, wa.Right, wa.Top));

        return ledges;
    }

    public Ledge? TryRefresh(Ledge ledge)
    {
        if (ledge.Hwnd is not IntPtr hwnd)
        {
            return ledge;
        }

        if (!Qualifies(hwnd) || !GetWindowRect(hwnd, out RECT r))
        {
            return null;
        }

        return new Ledge(hwnd, r.Left, r.Right, r.Top);
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
