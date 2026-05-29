using System.Windows.Media.Imaging;
using HyperPet.App.Pets;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;
using Xunit;

namespace HyperPet.App.Tests.Pets;

public class PetOverridesTests
{
    private static SpritePet MakePet()
    {
        var def = new PetDefinition
        {
            Id = "test",
            DisplayName = "Test",
            SpritesheetPath = "x.webp",
            FrameWidth = 1,
            FrameHeight = 1,
            States = new Dictionary<string, PetAnimationState>(StringComparer.OrdinalIgnoreCase)
            {
                ["idle"] = new PetAnimationState { Row = 0, Frames = 1, Fps = 4, Loop = true, PlayMode = PlayMode.Forward },
                ["runRight"] = new PetAnimationState { Row = 1, Frames = 1, Fps = 12, Loop = true, PlayMode = PlayMode.Reverse },
            },
        };
        var frames = new Dictionary<string, IReadOnlyList<BitmapSource>>(StringComparer.OrdinalIgnoreCase)
        {
            ["idle"] = new List<BitmapSource>(),
            ["runRight"] = new List<BitmapSource>(),
        };
        return new SpritePet(def, frames);
    }

    [Fact]
    public void SnapshotFps_CapturesOriginals()
    {
        var pet = MakePet();
        var snap = PetOverrides.SnapshotFps(pet);
        Assert.Equal(4, snap["idle"]);
        Assert.Equal(12, snap["runRight"]);
    }

    [Fact]
    public void SnapshotPlayMode_CapturesOriginals()
    {
        var pet = MakePet();
        var snap = PetOverrides.SnapshotPlayMode(pet);
        Assert.Equal(PlayMode.Forward, snap["idle"]);
        Assert.Equal(PlayMode.Reverse, snap["runRight"]);
    }

    [Fact]
    public void Apply_OverridesFpsAndPlayMode_FromSettings()
    {
        var pet = MakePet();
        var settings = HyperPetSettings.CreateDefault();
        settings.StateSpeedOverrides["test"] = new Dictionary<string, int> { ["idle"] = 9 };
        settings.StatePlayModeOverrides["test"] = new Dictionary<string, PlayMode> { ["idle"] = PlayMode.PingPong };

        PetOverrides.Apply(pet, settings);

        Assert.Equal(9, pet.Definition.States["idle"].Fps);
        Assert.Equal(PlayMode.PingPong, pet.Definition.States["idle"].PlayMode);
        Assert.Equal(12, pet.Definition.States["runRight"].Fps); // untouched
    }

    [Fact]
    public void Apply_IgnoresOutOfRangeFps()
    {
        var pet = MakePet();
        var settings = HyperPetSettings.CreateDefault();
        settings.StateSpeedOverrides["test"] = new Dictionary<string, int> { ["idle"] = 999 };

        PetOverrides.Apply(pet, settings);

        Assert.Equal(4, pet.Definition.States["idle"].Fps); // unchanged
    }

    [Fact]
    public void Apply_NoOverridesForPet_LeavesDefinitionUnchanged()
    {
        var pet = MakePet();
        var settings = HyperPetSettings.CreateDefault();

        PetOverrides.Apply(pet, settings);

        Assert.Equal(4, pet.Definition.States["idle"].Fps);
        Assert.Equal(PlayMode.Reverse, pet.Definition.States["runRight"].PlayMode);
    }
}
