using HyperPet.App.Views;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

public sealed class PetPlacementTests
{
    [Theory]
    [InlineData(true, 500, 300)]
    [InlineData(false, 80, 80)]
    public void Compute_UsesSavedPositionOnlyWhenEnabled(bool remember, double left, double top)
    {
        var settings = new HyperPetSettings
        {
            RememberPetPosition = remember,
            PetLeft = 500,
            PetTop = 300
        };

        var placement = PetPlacement.Compute(settings, 390, 260, new WorkArea(0, 0, 1920, 1040));

        Assert.Equal(left, placement.Left);
        Assert.Equal(top, placement.Top);
    }

    [Fact]
    public void Compute_PreservesPositionOnMonitorLeftOfPrimary()
    {
        var settings = new HyperPetSettings { PetLeft = -1500, PetTop = 230 };

        var placement = PetPlacement.Compute(settings, 390, 260, new WorkArea(-1920, 0, 0, 1040));

        Assert.Equal(-1500, placement.Left);
        Assert.Equal(230, placement.Top);
    }

    [Fact]
    public void Compute_ClampsPositionWhenSavedMonitorIsRemoved()
    {
        var settings = new HyperPetSettings { PetLeft = 2500, PetTop = 1300 };

        var placement = PetPlacement.Compute(settings, 390, 260, new WorkArea(0, 0, 1920, 1040));

        Assert.Equal(1530, placement.Left);
        Assert.Equal(780, placement.Top);
    }
}
