namespace HyperPet.App.Views;

public sealed record DesktopMonitor(string Id, string DisplayName, bool IsPrimary, WorkArea PhysicalWorkArea);

/// <summary>Enumerates physical monitor bounds and resolves the pet's preferred display.</summary>
public static class DesktopMonitors
{
    public static IReadOnlyList<DesktopMonitor> Enumerate()
        => System.Windows.Forms.Screen.AllScreens.Select((screen, index) =>
        {
            var area = screen.WorkingArea;
            string label = $"Display {index + 1} — {screen.DeviceName}";
            if (screen.Primary)
            {
                label += " (Primary)";
            }

            return new DesktopMonitor(screen.DeviceName, label, screen.Primary,
                new WorkArea(area.Left, area.Top, area.Right, area.Bottom));
        }).ToArray();

    public static DesktopMonitor? Resolve(IReadOnlyList<DesktopMonitor> monitors, string? preferredId,
        double physicalX, double physicalY)
    {
        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var preferred = monitors.FirstOrDefault(m => string.Equals(m.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        DesktopMonitor? nearest = null;
        double nearestDistance = double.PositiveInfinity;
        bool nearestContains = false;
        foreach (var monitor in monitors)
        {
            var area = monitor.PhysicalWorkArea;
            bool contains = physicalX >= area.Left && physicalX < area.Right &&
                            physicalY >= area.Top && physicalY < area.Bottom;
            double dx = Math.Max(Math.Max(area.Left - physicalX, 0), physicalX - area.Right);
            double dy = Math.Max(Math.Max(area.Top - physicalY, 0), physicalY - area.Bottom);
            double distance = dx * dx + dy * dy;
            if (nearest is null || (contains && !nearestContains) ||
                (contains == nearestContains && (distance < nearestDistance ||
                    (distance == nearestDistance && monitor.IsPrimary && !nearest.IsPrimary))))
            {
                nearest = monitor;
                nearestContains = contains;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public static WorkArea ToDips(DesktopMonitor monitor, double scaleX, double scaleY)
    {
        ValidateScale(scaleX, nameof(scaleX));
        ValidateScale(scaleY, nameof(scaleY));
        var area = monitor.PhysicalWorkArea;
        return new WorkArea(area.Left / scaleX, area.Top / scaleY, area.Right / scaleX, area.Bottom / scaleY);
    }

    internal static void ValidateScale(double scale, string parameterName)
    {
        if (!double.IsFinite(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "DPI scale must be finite and positive.");
        }
    }
}
