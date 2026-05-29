using HyperPet.App.Views;
using Xunit;

namespace HyperPet.App.Tests.Views;

public class MonitorWorkAreaTests
{
    [Fact]
    public void FromPhysical_NoScaling_PassesThrough()
    {
        var rc = new MonitorWorkArea.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1040 };
        var wa = MonitorWorkArea.FromPhysical(rc, 1.0, 1.0);
        Assert.Equal(1920, wa.Left);
        Assert.Equal(0, wa.Top);
        Assert.Equal(3840, wa.Right);
        Assert.Equal(1040, wa.Bottom);
    }

    [Fact]
    public void FromPhysical_DividesByDpiScale()
    {
        var rc = new MonitorWorkArea.RECT { Left = 0, Top = 0, Right = 3840, Bottom = 2160 };
        var wa = MonitorWorkArea.FromPhysical(rc, 2.0, 2.0);
        Assert.Equal(0, wa.Left);
        Assert.Equal(0, wa.Top);
        Assert.Equal(1920, wa.Right);
        Assert.Equal(1080, wa.Bottom);
    }
}
