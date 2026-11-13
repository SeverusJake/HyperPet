using System.Text.Json;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;

namespace HyperPet.Core.Settings;

public sealed class SettingsStore
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

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

    public async Task<SettingsLoadResult> LoadSessionAsync()
    {
        try { return new(await LoadAsync().ConfigureAwait(false), true, true, null); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(HyperPetSettings.CreateDefault(), false, false,
                "Saved settings could not be read. This session will not overwrite them or change Windows startup. " + exception.Message);
        }
    }

    public async Task SaveAsync(HyperPetSettings settings)
    {
        var snapshot = Sanitize(settings);
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(() => WriteSnapshot(snapshot)).ConfigureAwait(false); }
        finally { _writeGate.Release(); }
    }

    public void Save(HyperPetSettings settings)
    {
        var snapshot = Sanitize(settings);
        _writeGate.Wait();
        try { WriteSnapshot(snapshot); }
        finally { _writeGate.Release(); }
    }

    private void WriteSnapshot(HyperPetSettings snapshot)
    {
        string temporary = _settingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, snapshot, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_settingsPath)) File.Replace(temporary, _settingsPath, _settingsPath + ".bak");
            else File.Move(temporary, _settingsPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static Dictionary<string, Dictionary<string, T>> CleanOverrides<T>(Dictionary<string, Dictionary<string, T>>? values, Func<T, bool> valid)
    {
        var result = new Dictionary<string, Dictionary<string, T>>(StringComparer.OrdinalIgnoreCase);
        if (values is null) return result;
        foreach (var (pet, map) in values)
        {
            if (string.IsNullOrWhiteSpace(pet) || map is null) continue;
            var cleaned = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var (state, value) in map)
                if (!string.IsNullOrWhiteSpace(state) && valid(value)) cleaned[state] = value;
            if (cleaned.Count > 0) result[pet] = cleaned;
        }
        return result;
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
            RunningSpeed = Math.Clamp(settings.RunningSpeed, 1, 20),
            AlertsPaused = settings.AlertsPaused,
            ShowFullNotificationContent = settings.ShowFullNotificationContent,
            StartWithWindows = settings.StartWithWindows,
            RememberPetPosition = settings.RememberPetPosition,
            PreferredMonitorId = settings.PreferredMonitorId?.Trim() ?? string.Empty,
            PetLeft = double.IsFinite(settings.PetLeft) ? settings.PetLeft : 80,
            PetTop = double.IsFinite(settings.PetTop) ? settings.PetTop : 80,
            OpenAppOnBubbleClick = settings.OpenAppOnBubbleClick,
            ReactToWindowsNotifications = settings.ReactToWindowsNotifications,
            ReactToInAppNotifications = settings.ReactToInAppNotifications,
            WindowsNotificationPollIntervalSeconds = Math.Clamp(settings.WindowsNotificationPollIntervalSeconds, 5, 600),
            InAppNotificationPollIntervalSeconds = Math.Clamp(settings.InAppNotificationPollIntervalSeconds, 1, 60),
            DebugMode = settings.DebugMode,
            AutoUpdate = settings.AutoUpdate,
            QuietHoursEnabled = settings.QuietHoursEnabled,
            QuietHoursStart = QuietHoursSchedule.TryParse(settings.QuietHoursStart) is not null
                ? settings.QuietHoursStart.Trim()
                : "22:00",
            QuietHoursEnd = QuietHoursSchedule.TryParse(settings.QuietHoursEnd) is not null
                ? settings.QuietHoursEnd.Trim()
                : "07:00",
            MessagingApps = SanitizeMessagingApps(settings.MessagingApps),
            WatchedInAppProcesses = settings.WatchedInAppProcesses?.ToList() ?? new List<string> { "Zalo" },
            StateSpeedOverrides = CleanOverrides(settings.StateSpeedOverrides, fps => fps is >= 1 and <= 60),
            StatePlayModeOverrides = CleanOverrides(settings.StatePlayModeOverrides, mode => Enum.IsDefined(mode))
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
