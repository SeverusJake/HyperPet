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
            var backupPath = $"{_settingsPath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(_settingsPath, backupPath);
            return HyperPetSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(HyperPetSettings settings)
    {
        settings.SelectedPet = string.IsNullOrWhiteSpace(settings.SelectedPet)
            ? "Default"
            : settings.SelectedPet;
        settings.AlertDurationSeconds = Math.Clamp(settings.AlertDurationSeconds, 3, 30);

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
