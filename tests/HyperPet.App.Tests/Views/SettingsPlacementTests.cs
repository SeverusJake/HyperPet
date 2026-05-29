using HyperPet.App.Views;
using Xunit;

namespace HyperPet.App.Tests.Views;

public class SettingsPlacementTests
{
    private const double WaL = 0, WaT = 0, WaR = 1920, WaB = 1040;

    [Fact]
    public void Compute_PlacesLeftOfPet_WhenRoom()
    {
        var p = SettingsPlacement.Compute(800, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(800 - 8 - 500, p.Left);
        Assert.Equal(100, p.Top);
    }

    [Fact]
    public void Compute_FallsBackRight_WhenLeftUnderflows()
    {
        var p = SettingsPlacement.Compute(50, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(50 + 200 + 8, p.Left);
    }

    [Fact]
    public void Compute_ClampsRightIntoWorkArea_WhenPetFarRight()
    {
        var p = SettingsPlacement.Compute(60, 100, 200, 200, 500, 640, WaL, WaT, 600, WaB);
        Assert.Equal(100, p.Left);
    }

    [Fact]
    public void Compute_ClampsTop_WhenPetNearBottom()
    {
        var p = SettingsPlacement.Compute(800, 1000, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(WaB - 640, p.Top);
    }

    [Fact]
    public void Compute_LeftExactFit_StaysLeft()
    {
        var p = SettingsPlacement.Compute(508, 0, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(0, p.Left);
    }

    [Fact]
    public void Compute_RespectsNonZeroWorkAreaOrigin()
    {
        var p = SettingsPlacement.Compute(2200, 100, 200, 200, 500, 640, 1920, 0, 3840, 1040);
        Assert.Equal(2408, p.Left);
        Assert.Equal(100, p.Top);
    }
}
