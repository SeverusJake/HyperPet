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
        bool reactToMessagingApps,
        bool openAppOnBubbleClick,
        bool reactToWindowsNotifications,
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
        settings.AlertDurationSeconds = alertDurationSeconds;
        settings.StartWithWindows = startWithWindows;
        settings.ReactToMessagingApps = reactToMessagingApps;
        settings.OpenAppOnBubbleClick = openAppOnBubbleClick;
        settings.ReactToWindowsNotifications = reactToWindowsNotifications;
        settings.DebugMode = debugMode;
        settings.MessagingApps = messagingApps?.ToList() ?? MessagingAppRule.CreateDefaults().ToList();

        return true;
    }
}
