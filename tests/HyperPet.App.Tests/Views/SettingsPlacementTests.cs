using HyperPet.App.Views;
using Xunit;

namespace HyperPet.App.Tests.Views;

public class SettingsPlacementTests
{
    private const double WaL = 0, WaT = 0, WaR = 1920, WaB = 1040;

    [Fact]
    public void Compute_PlacesRightOfPet_WhenRoom()
    {
        var p = SettingsPlacement.Compute(300, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(300 + 200 + 8, p.Left);
        Assert.Equal(100, p.Top);
    }

    [Fact]
    public void Compute_FallsBackLeft_WhenRightOverflows()
    {
        var p = SettingsPlacement.Compute(1700, 100, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(1700 - 8 - 500, p.Left);
    }

    [Fact]
    public void Compute_ClampsLeftIntoWorkArea_WhenPetFarLeft()
    {
        var p = SettingsPlacement.Compute(10, 100, 200, 200, 500, 640, WaL, WaT, 600, WaB);
        Assert.Equal(0, p.Left);
    }

    [Fact]
    public void Compute_ClampsTop_WhenPetNearBottom()
    {
        var p = SettingsPlacement.Compute(300, 1000, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(WaB - 640, p.Top);
    }

    [Fact]
    public void Compute_RightExactFit_StaysRight()
    {
        var p = SettingsPlacement.Compute(1212, 0, 200, 200, 500, 640, WaL, WaT, WaR, WaB);
        Assert.Equal(1212 + 200 + 8, p.Left);
    }
}
