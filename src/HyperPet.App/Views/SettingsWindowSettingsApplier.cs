using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Views;

public static class SettingsWindowSettingsApplier
{
    public static bool TryApply(
        HyperPetSettings settings,
        bool showFullNotificationContent,
        PetBehaviorMode petBehaviorMode,
        int alertDurationSeconds,
        bool startWithWindows,
        bool openAppOnBubbleClick,
        bool reactToWindowsNotifications,
        bool reactToInAppNotifications,
        int windowsPollIntervalSeconds,
        int inAppPollIntervalSeconds,
        int petSize,
        int runningSpeed,
        bool debugMode,
        IReadOnlyList<MessagingAppRule> messagingApps,
        Action<bool> applyStartupSetting,
        Action<string> showStartupWarning)
    {
        bool previousStartWithWindows = settings.StartWithWindows;

        if (startWithWindows != previousStartWithWindows)
        {
            try
            {
                applyStartupSetting(startWithWindows);
            }
            catch (Exception exception)
            {
                settings.StartWithWindows = previousStartWithWindows;
                showStartupWarning(
                    $"HyperPet could not update the Windows startup setting. Startup was left unchanged.\n\n{exception.Message}");
                return false;
            }
        }

        settings.ShowFullNotificationContent = showFullNotificationContent;
        settings.PetBehaviorMode = petBehaviorMode;
        settings.AlertDurationSeconds = Math.Clamp(alertDurationSeconds, 1, 600);
        settings.StartWithWindows = startWithWindows;
        settings.OpenAppOnBubbleClick = openAppOnBubbleClick;
        settings.ReactToWindowsNotifications = reactToWindowsNotifications;
        settings.ReactToInAppNotifications = reactToInAppNotifications;
        settings.WindowsNotificationPollIntervalSeconds = Math.Clamp(windowsPollIntervalSeconds, 5, 600);
        settings.InAppNotificationPollIntervalSeconds = Math.Clamp(inAppPollIntervalSeconds, 1, 60);
        settings.PetSize = Math.Clamp(petSize, 1, 10);
        settings.RunningSpeed = Math.Clamp(runningSpeed, 1, 20);
        settings.DebugMode = debugMode;
        settings.MessagingApps = messagingApps?.ToList() ?? MessagingAppRule.CreateDefaults().ToList();

        return true;
    }
}
