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
            OnlyMessagingApps = true,
            OpenAppOnBubbleClick = false
        };
        string? warning = null;

        bool applied = SettingsWindowSettingsApplier.TryApply(
            settings,
            showFullNotificationContent: true,
            petBehaviorMode: PetBehaviorMode.Desktop,
            alertDurationSeconds: 20,
            startWithWindows: true,
            onlyMessagingApps: false,
            openAppOnBubbleClick: true,
            enableFrameControls: false,
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
        Assert.True(settings.OnlyMessagingApps);
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
            OnlyMessagingApps = false,
            OpenAppOnBubbleClick = false
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
            onlyMessagingApps: true,
            openAppOnBubbleClick: true,
            enableFrameControls: true,
            messagingApps: newApps,
            applyStartupSetting: value => appliedStartup = value,
            showStartupWarning: _ => throw new InvalidOperationException("warning should not show"));

        Assert.True(applied);
        Assert.True(appliedStartup);
        Assert.True(settings.ShowFullNotificationContent);
        Assert.True(settings.StartWithWindows);
        Assert.Equal(PetBehaviorMode.Desktop, settings.PetBehaviorMode);
        Assert.Equal(20, settings.AlertDurationSeconds);
        Assert.True(settings.OnlyMessagingApps);
        Assert.True(settings.OpenAppOnBubbleClick);
        Assert.True(settings.EnableFrameControls);
        Assert.Equal(2, settings.MessagingApps.Count);
        Assert.Contains(settings.MessagingApps, app => app.DisplayName == "Telegram");
    }
}

