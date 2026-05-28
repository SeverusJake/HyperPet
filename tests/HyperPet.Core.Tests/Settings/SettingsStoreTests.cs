using HyperPet.Core.Notifications;
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
        Assert.True(settings.ReactToWindowsNotifications);
        Assert.True(settings.ReactToInAppNotifications);
        Assert.Equal(30, settings.WindowsNotificationPollIntervalSeconds);
        Assert.Equal(2, settings.InAppNotificationPollIntervalSeconds);
        Assert.True(settings.OpenAppOnBubbleClick);
        Assert.False(settings.AutoUpdate);
        Assert.Equal(2, settings.RunningSpeed);
        Assert.Equal(3, settings.MessagingApps.Count);
        Assert.Contains(settings.MessagingApps, app => app.DisplayName == "Discord");
        Assert.Contains(settings.MessagingApps, app => app.DisplayName == "Zalo");
        Assert.Contains(settings.MessagingApps, app => app.DisplayName == "Messenger");
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
            PetTop = 220,
            ReactToWindowsNotifications = false,
            ReactToInAppNotifications = false,
            WindowsNotificationPollIntervalSeconds = 15,
            InAppNotificationPollIntervalSeconds = 5,
            OpenAppOnBubbleClick = true,
            AutoUpdate = true,
            RunningSpeed = 7,
            MessagingApps = new List<MessagingAppRule>
            {
                new("Telegram", new[] { "Telegram" }),
                new("Slack", new[] { "Slack" }, enabled: false)
            }
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
        Assert.False(actual.ReactToWindowsNotifications);
        Assert.False(actual.ReactToInAppNotifications);
        Assert.Equal(15, actual.WindowsNotificationPollIntervalSeconds);
        Assert.Equal(5, actual.InAppNotificationPollIntervalSeconds);
        Assert.True(actual.OpenAppOnBubbleClick);
        Assert.True(actual.AutoUpdate);
        Assert.Equal(7, actual.RunningSpeed);
        Assert.Equal(2, actual.MessagingApps.Count);
        Assert.Contains(actual.MessagingApps, app => app.DisplayName == "Telegram" && app.Enabled);
        Assert.Contains(actual.MessagingApps, app => app.DisplayName == "Slack" && !app.Enabled);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsDesktopBehaviorMode()
    {
        var directory = CreateTempDirectory();
        var store = new SettingsStore(directory);
        var expected = new HyperPetSettings { PetBehaviorMode = PetBehaviorMode.Desktop };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(PetBehaviorMode.Desktop, actual.PetBehaviorMode);
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
    [InlineData("", 0, "miku-kimono", 1)]
    [InlineData("  ", 601, "miku-kimono", 600)]
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
