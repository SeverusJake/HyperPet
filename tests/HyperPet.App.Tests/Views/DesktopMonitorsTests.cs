using HyperPet.App.Views;

namespace HyperPet.App.Tests.Views;

public class DesktopMonitorsTests
{
    private static readonly DesktopMonitor Primary = new("primary", "Display 1", true, new WorkArea(0, 0, 1920, 1040));
    private static readonly DesktopMonitor Left = new("left", "Display 2", false, new WorkArea(-1920, -300, 0, 780));

    [Fact]
    public void Resolve_UsesAvailablePreferredMonitorIgnoringCase()
        => Assert.Same(Left, DesktopMonitors.Resolve([Primary, Left], "LEFT", 100, 100));

    [Fact]
    public void Resolve_MissingPreferredMonitorFallsBackToContainingNegativeOrigin()
        => Assert.Same(Left, DesktopMonitors.Resolve([Primary, Left], "removed", -1000, -100));

    [Fact]
    public void Resolve_UsesNearestRectangleWhenPointIsOutsideAllMonitors()
        => Assert.Same(Left, DesktopMonitors.Resolve([Primary, Left], null, -2100, -500));

    [Fact]
    public void Resolve_PrefersPrimaryWhenRectangleDistancesTie()
    {
        var primary = new DesktopMonitor("primary", "Display 1", true, new WorkArea(0, 0, 100, 100));
        var left = new DesktopMonitor("left", "Display 2", false, new WorkArea(-200, 0, -100, 100));
        Assert.Same(primary, DesktopMonitors.Resolve([left, primary], null, -50, 50));
    }

    [Fact]
    public void Resolve_AtSharedEdgeUsesMonitorContainingPoint()
        => Assert.Same(Primary, DesktopMonitors.Resolve([Left, Primary], null, 0, 100));

    [Fact]
    public void Resolve_EmptyListReturnsNull()
        => Assert.Null(DesktopMonitors.Resolve([], "removed", 100, 100));

    [Fact]
    public void ToDips_ConvertsNegativeOriginAt150Percent()
    {
        var monitor = new DesktopMonitor("left", "Display 2", false, new WorkArea(-1920, -300, 0, 780));
        Assert.Equal(new WorkArea(-1280, -200, 0, 520), DesktopMonitors.ToDips(monitor, 1.5, 1.5));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.PositiveInfinity)]
    public void ToDips_RejectsInvalidScales(double x, double y)
        => Assert.Throws<ArgumentOutOfRangeException>(() => DesktopMonitors.ToDips(Primary, x, y));
}
