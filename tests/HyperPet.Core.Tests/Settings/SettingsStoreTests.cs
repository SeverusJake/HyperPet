using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly List<string> _tempDirectories = [];

    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsDefaultsAndCreatesFile()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal("miku-kimono", settings.SelectedPet);
        Assert.Equal(PetBehaviorMode.Calm, settings.PetBehaviorMode);
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
            PetBehaviorMode = PetBehaviorMode.Desktop,
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
        Assert.Equal(expected.PetBehaviorMode, actual.PetBehaviorMode);
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
        var settingsPath = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ broken json");
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal("miku-kimono", settings.SelectedPet);
        Assert.Equal(PetBehaviorMode.Calm, settings.PetBehaviorMode);
        Assert.Equal(8, settings.AlertDurationSeconds);
        Assert.True(File.Exists(settingsPath));
        var savedSettings = await store.LoadAsync();
        Assert.Equal("miku-kimono", savedSettings.SelectedPet);
        Assert.Equal(PetBehaviorMode.Calm, savedSettings.PetBehaviorMode);
        Assert.Equal(8, savedSettings.AlertDurationSeconds);
        Assert.True(Directory.GetFiles(directory, "settings.json.corrupt-*").Length == 1);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonCorruptRepeatedly_CreatesUniqueBackups()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "settings.json");
        var store = new SettingsStore(directory);

        await File.WriteAllTextAsync(settingsPath, "{ broken json");
        await store.LoadAsync();

        await File.WriteAllTextAsync(settingsPath, "{ broken json again");
        await store.LoadAsync();

        Assert.Equal(2, Directory.GetFiles(directory, "settings.json.corrupt-*").Length);
    }

    [Fact]
    public async Task LoadAsync_WhenPetBehaviorModeIsInvalidNumericValue_ReturnsCalm()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), """
        {
          "SelectedPet": "miku-kimono",
          "PetBehaviorMode": 999,
          "AlertDurationSeconds": 8
        }
        """);
        var store = new SettingsStore(directory);

        var settings = await store.LoadAsync();

        Assert.Equal(PetBehaviorMode.Calm, settings.PetBehaviorMode);
    }

    [Theory]
    [InlineData("", 2, "miku-kimono", 3)]
    [InlineData("  ", 31, "miku-kimono", 30)]
    public async Task SaveAsync_WhenSettingsNeedSanitizing_SavesSanitizedCopyWithoutMutatingOriginal(
        string selectedPet,
        int alertDurationSeconds,
        string expectedSelectedPet,
        int expectedAlertDurationSeconds)
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);
        var settings = new HyperPetSettings
        {
            SelectedPet = selectedPet,
            AlertDurationSeconds = alertDurationSeconds
        };

        await store.SaveAsync(settings);
        var actual = await store.LoadAsync();

        Assert.Equal(expectedSelectedPet, actual.SelectedPet);
        Assert.Equal(expectedAlertDurationSeconds, actual.AlertDurationSeconds);
        Assert.Equal(selectedPet, settings.SelectedPet);
        Assert.Equal(alertDurationSeconds, settings.AlertDurationSeconds);
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HyperPet.Tests", Guid.NewGuid().ToString("N"));
        _tempDirectories.Add(directory);
        return directory;
    }
}
