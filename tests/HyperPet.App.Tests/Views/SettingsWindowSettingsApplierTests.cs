using HyperPet.App.Views;
using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;
using HyperPet.Core.Settings;

namespace HyperPet.App.Tests.Views;

public sealed class SettingsWindowSettingsApplierTests
{
    [Fact]
    public void TryApply_ReturnsFalseAndPreservesSettings_WhenStartupApplyFails()
    {
        var settings = new HyperPetSettings
        {
            ShowFullNotificationContent = false,
            StartWithWindows = false,
            PetBehaviorMode = PetBehaviorMode.Calm,
            AlertDurationSeconds = 8,
            OpenAppOnBubbleClick = false,
            ReactToWindowsNotifications = true,
            ReactToInAppNotifications = true,
            WindowsNotificationPollIntervalSeconds = 30,
            InAppNotificationPollIntervalSeconds = 2
        };
        string? warning = null;

        bool applied = SettingsWindowSettingsApplier.TryApply(
            settings,
            showFullNotificationContent: true,
            petBehaviorMode: PetBehaviorMode.Desktop,
            alertDurationSeconds: 20,
            startWithWindows: true,
            openAppOnBubbleClick: true,
            reactToWindowsNotifications: false,
            reactToInAppNotifications: false,
            windowsPollIntervalSeconds: 15,
            inAppPollIntervalSeconds: 5,
            petSize: 7,
            debugMode: false,
            messagingApps: new List<MessagingAppRule>
            {
                new("Telegram", new[] { "Telegram" })
            },
            applyStartupSetting: _ => throw new InvalidOperationException("registry denied"),
            showStartupWarning: message => warning = message);

        Assert.False(applied);
        Assert.False(settings.ShowFullNotificationContent);
        Assert.False(settings.StartWithWindows);
        Assert.Equal(PetBehaviorMode.Calm, settings.PetBehaviorMode);
        Assert.Equal(8, settings.AlertDurationSeconds);
        Assert.True(settings.ReactToWindowsNotifications);
        Assert.True(settings.ReactToInAppNotifications);
        Assert.Equal(30, settings.WindowsNotificationPollIntervalSeconds);
        Assert.Equal(2, settings.InAppNotificationPollIntervalSeconds);
        Assert.False(settings.OpenAppOnBubbleClick);
        Assert.Contains("Startup was left unchanged", warning);
    }

    [Fact]
    public void TryApply_WritesConsistentControlValues_WhenStartupApplySucceeds()
    {
        var settings = new HyperPetSettings
        {
            ShowFullNotificationContent = false,
            StartWithWindows = false,
            PetBehaviorMode = PetBehaviorMode.Calm,
            AlertDurationSeconds = 8,
            OpenAppOnBubbleClick = false,
            ReactToWindowsNotifications = true,
            ReactToInAppNotifications = true,
            WindowsNotificationPollIntervalSeconds = 30,
            InAppNotificationPollIntervalSeconds = 2
        };
        bool? appliedStartup = null;

        var newApps = new List<MessagingAppRule>
        {
            new("Discord", new[] { "Discord" }),
            new("Telegram", new[] { "Telegram" })
        };

        bool applied = SettingsWindowSettingsApplier.TryApply(
            settings,
            showFullNotificationContent: true,
            petBehaviorMode: PetBehaviorMode.Desktop,
            alertDurationSeconds: 20,
            startWithWindows: true,
            openAppOnBubbleClick: true,
            reactToWindowsNotifications: false,
            reactToInAppNotifications: false,
            windowsPollIntervalSeconds: 45,
            inAppPollIntervalSeconds: 4,
            petSize: 10,
            debugMode: true,
            messagingApps: newApps,
            applyStartupSetting: value => appliedStartup = value,
            showStartupWarning: _ => throw new InvalidOperationException("warning should not show"));

        Assert.True(applied);
        Assert.True(appliedStartup);
        Assert.True(settings.ShowFullNotificationContent);
        Assert.True(settings.StartWithWindows);
        Assert.Equal(PetBehaviorMode.Desktop, settings.PetBehaviorMode);
        Assert.Equal(20, settings.AlertDurationSeconds);
        Assert.True(settings.OpenAppOnBubbleClick);
        Assert.False(settings.ReactToWindowsNotifications);
        Assert.False(settings.ReactToInAppNotifications);
        Assert.Equal(45, settings.WindowsNotificationPollIntervalSeconds);
        Assert.Equal(4, settings.InAppNotificationPollIntervalSeconds);
        Assert.Equal(10, settings.PetSize);
        Assert.True(settings.DebugMode);
        Assert.Equal(2, settings.MessagingApps.Count);
        Assert.Contains(settings.MessagingApps, app => app.DisplayName == "Telegram");
    }
}
