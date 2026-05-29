using System.Runtime.InteropServices;

namespace HyperPet.App.Views;

public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Resolves the work area (in DIPs) of the monitor that contains a given DIP
/// point, so windows can be placed on the same monitor as the pet rather than
/// always the primary one (which is what SystemParameters.WorkArea reports).
/// </summary>
public static class MonitorWorkArea
{
    private const int MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// Work area of the monitor under the given DIP point. Returns null when
    /// the OS lookup fails (caller should fall back to the primary work area).
    /// </summary>
    public static WorkArea? ForPoint(double dipX, double dipY, double dpiScaleX, double dpiScaleY)
    {
        if (dpiScaleX <= 0 || dpiScaleY <= 0)
        {
            return null;
        }

        var pt = new POINT
        {
            X = (int)Math.Round(dipX * dpiScaleX),
            Y = (int)Math.Round(dipY * dpiScaleY),
        };

        IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return null;
        }

        return FromPhysical(info.rcWork, dpiScaleX, dpiScaleY);
    }

    /// <summary>
    /// Pure conversion of a physical-pixel work rect to DIPs. Exposed for tests.
    /// </summary>
    internal static WorkArea FromPhysical(RECT rcWork, double dpiScaleX, double dpiScaleY)
    {
        return new WorkArea(
            rcWork.Left / dpiScaleX,
            rcWork.Top / dpiScaleY,
            rcWork.Right / dpiScaleX,
            rcWork.Bottom / dpiScaleY);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
