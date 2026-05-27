using HyperPet.Core.Notifications;
using HyperPet.Core.Pet;
using HyperPet.Core.Settings;

namespace HyperPet.Core.Tests.Pet;

public sealed class PetControllerTests
{
    [Fact]
    public void HandleNotification_WhenAlertsEnabled_ShowsAlert()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        var notification = CreateNotification();

        var alert = controller.HandleNotification(notification, settings);

        Assert.Equal(PetState.Alerting, controller.State);
        Assert.NotNull(alert);
        Assert.Equal("Discord", alert.AppName);
        Assert.Equal("Friend", alert.Title);
        Assert.Equal("hello from discord", alert.Body);
        Assert.True(alert.CanActivate);
        Assert.Equal("Discord.Discord", alert.AppUserModelId);
    }

    [Fact]
    public void HandleNotification_WhenAlertsPaused_ReturnsNullAndKeepsIdle()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        settings.AlertsPaused = true;

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.Null(alert);
        Assert.Equal(PetState.Idle, controller.State);
    }

    [Fact]
    public void HandleNotification_WhenFullContentDisabled_HidesTitleAndBody()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        settings.ShowFullNotificationContent = false;

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.NotNull(alert);
        Assert.Equal("Discord", alert.AppName);
        Assert.Equal("Notification", alert.Title);
        Assert.Equal(string.Empty, alert.Body);
    }

    [Fact]
    public void HandleNotification_WhenMatchingAppRuleDisabled_FiltersOut()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        var discordRule = settings.MessagingApps.First(r => r.DisplayName == "Discord");
        discordRule.Enabled = false;

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.Null(alert);
        Assert.Equal(PetState.Idle, controller.State);
    }

    [Fact]
    public void HandleNotification_WhenAppNotInList_StillAllowed()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();

        var notification = new HyperNotification(
            "outlook-1",
            "Outlook",
            "Boss",
            "ping",
            DateTimeOffset.UtcNow,
            canActivate: false,
            appUserModelId: "Microsoft.Office.OUTLOOK");

        var alert = controller.HandleNotification(notification, settings);

        Assert.NotNull(alert);
    }

    [Fact]
    public void HandleNotification_WhenMatchingAppRuleEnabled_Allowed()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.NotNull(alert);
    }

    [Fact]
    public void DismissAlert_ReturnsToIdle()
    {
        var controller = new PetController();
        controller.HandleNotification(CreateNotification(), HyperPetSettings.CreateDefault());

        controller.DismissAlert();

        Assert.Equal(PetState.Idle, controller.State);
        Assert.Null(controller.CurrentAlert);
    }

    private static HyperNotification CreateNotification()
    {
        return new HyperNotification(
            "discord-1",
            "Discord",
            "Friend",
            "hello from discord",
            DateTimeOffset.Parse("2026-05-14T10:00:00+07:00"),
            canActivate: true,
            appUserModelId: "Discord.Discord");
    }
}

