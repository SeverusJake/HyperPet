using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Settings;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsDefaultsAndCreatesFile()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal("Default", settings.SelectedPet);
        Assert.Equal(8, settings.AlertDurationSeconds);
        Assert.False(settings.AlertsPaused);
        Assert.True(settings.ShowFullNotificationContent);
        Assert.False(settings.StartWithWindows);
        Assert.True(File.Exists(Path.Combine(directory, "settings.json")));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsValues()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);
        var expected = new HyperPetSettings
        {
            SelectedPet = "PixelCat",
            AlertDurationSeconds = 12,
            AlertsPaused = true,
            ShowFullNotificationContent = false,
            StartWithWindows = true,
            PetLeft = 140,
            PetTop = 220
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected.SelectedPet, actual.SelectedPet);
        Assert.Equal(expected.AlertDurationSeconds, actual.AlertDurationSeconds);
        Assert.Equal(expected.AlertsPaused, actual.AlertsPaused);
        Assert.Equal(expected.ShowFullNotificationContent, actual.ShowFullNotificationContent);
        Assert.Equal(expected.StartWithWindows, actual.StartWithWindows);
        Assert.Equal(expected.PetLeft, actual.PetLeft);
        Assert.Equal(expected.PetTop, actual.PetTop);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonCorrupt_BacksUpFileAndReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{ broken json");
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal("Default", settings.SelectedPet);
        Assert.True(Directory.GetFiles(directory, "settings.json.corrupt-*").Length == 1);
    }

    private static string CreateTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "HyperPet.Tests", Guid.NewGuid().ToString("N"));
    }
}
