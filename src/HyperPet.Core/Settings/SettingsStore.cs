using System.Text.Json;

namespace HyperPet.Core.Settings;

public sealed class SettingsStore
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStore(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsPath = Path.Combine(settingsDirectory, SettingsFileName);
    }

    public async Task<HyperPetSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = HyperPetSettings.CreateDefault();
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<HyperPetSettings>(stream, JsonOptions)
                ?? HyperPetSettings.CreateDefault();
        }
        catch (JsonException)
        {
            var backupPath = $"{_settingsPath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}";
            File.Move(_settingsPath, backupPath);
            var defaults = HyperPetSettings.CreateDefault();
            await SaveAsync(defaults);
            return defaults;
        }
    }

    public async Task SaveAsync(HyperPetSettings settings)
    {
        var sanitized = new HyperPetSettings
        {
            SelectedPet = string.IsNullOrWhiteSpace(settings.SelectedPet)
                ? "Default"
                : settings.SelectedPet,
            AlertDurationSeconds = Math.Clamp(settings.AlertDurationSeconds, 3, 30),
            AlertsPaused = settings.AlertsPaused,
            ShowFullNotificationContent = settings.ShowFullNotificationContent,
            StartWithWindows = settings.StartWithWindows,
            PetLeft = settings.PetLeft,
            PetTop = settings.PetTop
        };

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, sanitized, JsonOptions);
    }
}
