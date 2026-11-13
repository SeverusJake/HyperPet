using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Settings;

public sealed class SettingsSafetyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HyperPet.SettingsSafety", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task NullNestedOverrides_AreRemoved()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{\"StateSpeedOverrides\":{\"pet\":null},\"StatePlayModeOverrides\":{\"pet\":null}}");
        var settings = await new SettingsStore(_directory).LoadAsync();
        Assert.Empty(settings.StateSpeedOverrides); Assert.Empty(settings.StatePlayModeOverrides);
    }
    [Fact]
    public void SavingAgain_PreservesLastGoodBackup()
    {
        var store = new SettingsStore(_directory);
        store.Save(new HyperPetSettings { SelectedPet = "first" });
        store.Save(new HyperPetSettings { SelectedPet = "second" });
        Assert.Contains("first", File.ReadAllText(Path.Combine(_directory, "settings.json.bak")));
        Assert.Contains("second", File.ReadAllText(Path.Combine(_directory, "settings.json")));
    }
    [Fact]
    public async Task NonFiniteCoordinates_AreSanitized()
    {
        var store = new SettingsStore(_directory);
        store.Save(new HyperPetSettings { PetLeft = double.NaN, PetTop = double.PositiveInfinity });
        var loaded = await store.LoadAsync();
        Assert.True(double.IsFinite(loaded.PetLeft)); Assert.True(double.IsFinite(loaded.PetTop));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
