using System.Text.Json;
using HyperPet.Core.Settings;
namespace HyperPet.App.Views;
internal static class SettingsDraft
{
    public static HyperPetSettings Clone(HyperPetSettings source)
    {
        var clone = JsonSerializer.Deserialize<HyperPetSettings>(JsonSerializer.Serialize(source))!;
        clone.StateSpeedOverrides = CloneOverrides(source.StateSpeedOverrides);
        clone.StatePlayModeOverrides = CloneOverrides(source.StatePlayModeOverrides);
        return clone;
    }

    private static Dictionary<string, Dictionary<string, T>> CloneOverrides<T>(Dictionary<string, Dictionary<string, T>> values)
    {
        var result = new Dictionary<string, Dictionary<string, T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pet, states) in values)
        {
            var copy = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var (state, value) in states) copy[state] = value;
            result[pet] = copy;
        }
        return result;
    }
    // Candidate owns its nested collections. Preserve the shared settings object's identity.
    public static void CopyTo(HyperPetSettings source, HyperPetSettings target)
    {
        target.SelectedPet = source.SelectedPet;
        target.PetBehaviorMode = source.PetBehaviorMode;
        target.AlertDurationSeconds = source.AlertDurationSeconds;
        target.PetSize = source.PetSize;
        target.RunningSpeed = source.RunningSpeed;
        target.AlertsPaused = source.AlertsPaused;
        target.ShowFullNotificationContent = source.ShowFullNotificationContent;
        target.StartWithWindows = source.StartWithWindows;
        target.RememberPetPosition = source.RememberPetPosition;
        target.PreferredMonitorId = source.PreferredMonitorId;
        target.PetLeft = source.PetLeft;
        target.PetTop = source.PetTop;
        target.OpenAppOnBubbleClick = source.OpenAppOnBubbleClick;
        target.ReactToWindowsNotifications = source.ReactToWindowsNotifications;
        target.ReactToInAppNotifications = source.ReactToInAppNotifications;
        target.WindowsNotificationPollIntervalSeconds = source.WindowsNotificationPollIntervalSeconds;
        target.InAppNotificationPollIntervalSeconds = source.InAppNotificationPollIntervalSeconds;
        target.DebugMode = source.DebugMode;
        target.AutoUpdate = source.AutoUpdate;
        target.QuietHoursEnabled = source.QuietHoursEnabled;
        target.QuietHoursStart = source.QuietHoursStart;
        target.QuietHoursEnd = source.QuietHoursEnd;
        target.MessagingApps = source.MessagingApps;
        target.WatchedInAppProcesses = source.WatchedInAppProcesses;
        target.StateSpeedOverrides = source.StateSpeedOverrides;
        target.StatePlayModeOverrides = source.StatePlayModeOverrides;
    }
}
