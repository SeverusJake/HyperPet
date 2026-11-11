using HyperPet.App.Pets.Roaming;
using HyperPet.App.Views;
using HyperPet.App.Tests.TestSupport;
using System.Windows;
using System.Windows.Interop;

namespace HyperPet.App.Tests.Pets.Roaming;

[Collection("WPF UI")]
public class WindowLedgeConversionTests
{
    [Fact]
    public Task TryRefresh_ClosedWindowDoesNotRemainInCachedRecoveryLedges() => StaThread.RunAsync(() =>
    {
        var window = new Window { Width = 400, Height = 300, ShowActivated = false };
        try
        {
            window.Show();
            var hwnd = new WindowInteropHelper(window).Handle;
            var provider = new WindowLedgeProvider(IntPtr.Zero, () => DateTime.UnixEpoch);
            var ledge = Assert.Single(provider.GetLedges().Where(l => l.Hwnd == hwnd));
            window.Close();

            Assert.Null(provider.TryRefresh(ledge));
            Assert.DoesNotContain(provider.GetLedges(), l => l.Hwnd == hwnd);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ToWorkAreaLedge_ConvertsPhysicalCoordinatesAt150PercentAndClipsNegativeOrigin()
    {
        var ledge = new Ledge(new IntPtr(123), -2100, -150, 600);
        var area = new WorkArea(-1280, -200, -200, 520);
        Assert.Equal(new Ledge(new IntPtr(123), -1280, -200, 400),
            WindowLedgeProvider.ToWorkAreaLedge(ledge, area, 1.5, 1.5));
    }

    [Theory]
    [InlineData(150, 600, 300)]
    [InlineData(-1800, -300, -450)]
    [InlineData(-1800, -300, 900)]
    [InlineData(-2100, -1900, 300)]
    public void ToWorkAreaLedge_OmitsLedgesOutsideWorkAreaOrTooNarrow(double left, double right, double top)
    {
        Assert.Null(WindowLedgeProvider.ToWorkAreaLedge(new Ledge(new IntPtr(123), left, right, top),
            new WorkArea(-1280, -200, 0, 520), 1.5, 1.5));
    }

    [Fact]
    public void ToWorkAreaLedge_OmitsSurfaceThatWouldPlacePetAboveWorkArea()
    {
        Assert.Null(WindowLedgeProvider.ToWorkAreaLedge(new Ledge(new IntPtr(123), -1800, -300, 150),
            new WorkArea(-1280, 0, 0, 720), 1.5, 1.5, 200));
    }

    [Fact]
    public void ToWorkAreaLedge_AcceptsSurfaceAtPetVisibilityBoundary()
    {
        Assert.Equal(new Ledge(new IntPtr(123), -1200, -200, 200),
            WindowLedgeProvider.ToWorkAreaLedge(new Ledge(new IntPtr(123), -1800, -300, 300),
                new WorkArea(-1280, 0, 0, 720), 1.5, 1.5, 200));
    }
}
