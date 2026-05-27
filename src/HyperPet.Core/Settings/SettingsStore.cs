using System.Text.Json;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;

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
            await SaveAsync(defaults).ConfigureAwait(false);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<HyperPetSettings>(stream, JsonOptions).ConfigureAwait(false)
                ?? HyperPetSettings.CreateDefault();
            return Sanitize(settings);
        }
        catch (JsonException)
        {
            var backupPath = $"{_settingsPath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}";
            File.Move(_settingsPath, backupPath);
            var defaults = HyperPetSettings.CreateDefault();
            await SaveAsync(defaults).ConfigureAwait(false);
            return defaults;
        }
    }

    public async Task SaveAsync(HyperPetSettings settings)
    {
        var sanitized = Sanitize(settings);

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, sanitized, JsonOptions).ConfigureAwait(false);
    }

    public void Save(HyperPetSettings settings)
    {
        var sanitized = Sanitize(settings);

        using var stream = File.Create(_settingsPath);
        JsonSerializer.Serialize(stream, sanitized, JsonOptions);
    }

    private static HyperPetSettings Sanitize(HyperPetSettings settings)
    {
        return new HyperPetSettings
        {
            SelectedPet = string.IsNullOrWhiteSpace(settings.SelectedPet)
                ? "miku-kimono"
                : settings.SelectedPet,
            PetBehaviorMode = Enum.IsDefined(settings.PetBehaviorMode)
                ? settings.PetBehaviorMode
                : PetBehaviorMode.Calm,
            AlertDurationSeconds = Math.Clamp(settings.AlertDurationSeconds, 1, 600),
            PetSize = Math.Clamp(settings.PetSize, 1, 10),
            AlertsPaused = settings.AlertsPaused,
            ShowFullNotificationContent = settings.ShowFullNotificationContent,
            StartWithWindows = settings.StartWithWindows,
            PetLeft = settings.PetLeft,
            PetTop = settings.PetTop,
            OpenAppOnBubbleClick = settings.OpenAppOnBubbleClick,
            ReactToWindowsNotifications = settings.ReactToWindowsNotifications,
            ReactToInAppNotifications = settings.ReactToInAppNotifications,
            WindowsNotificationPollIntervalSeconds = Math.Clamp(settings.WindowsNotificationPollIntervalSeconds, 5, 600),
            InAppNotificationPollIntervalSeconds = Math.Clamp(settings.InAppNotificationPollIntervalSeconds, 1, 60),
            DebugMode = settings.DebugMode,
            MessagingApps = SanitizeMessagingApps(settings.MessagingApps),
            WatchedInAppProcesses = settings.WatchedInAppProcesses?.ToList() ?? new List<string> { "Zalo" }
        };
    }

    private static List<MessagingAppRule> SanitizeMessagingApps(List<MessagingAppRule>? rules)
    {
        if (rules is null || rules.Count == 0)
        {
            return MessagingAppRule.CreateDefaults().ToList();
        }

        var sanitized = new List<MessagingAppRule>(rules.Count);
        foreach (var rule in rules)
        {
            if (rule is null)
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(rule.DisplayName)
                ? string.Empty
                : rule.DisplayName.Trim();
            var patterns = (rule.MatchPatterns ?? new List<string>())
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .Select(pattern => pattern.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrEmpty(displayName) && patterns.Count == 0)
            {
                continue;
            }

            sanitized.Add(new MessagingAppRule
            {
                DisplayName = string.IsNullOrEmpty(displayName)
                    ? patterns[0]
                    : displayName,
                MatchPatterns = patterns,
                Enabled = rule.Enabled
            });
        }

        return sanitized.Count == 0
            ? MessagingAppRule.CreateDefaults().ToList()
            : sanitized;
    }
}
