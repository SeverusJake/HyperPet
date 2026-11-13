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

        Assert.NotNull(alert);
        Assert.Equal("Discord", alert.AppName);
        Assert.Equal("Friend", alert.Title);
        Assert.Equal("hello from discord", alert.Body);
        Assert.Equal("Discord.Discord", alert.AppUserModelId);
    }

    [Fact]
    public void HandleNotification_WhenAlertsPaused_ReturnsNull()
    {
        var controller = new PetController();
        var settings = HyperPetSettings.CreateDefault();
        settings.AlertsPaused = true;

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.Null(alert);
    }

    [Fact]
    public void HandleNotification_WhenInsideQuietHours_ReturnsNull()
    {
        // Clock fixed at 23:00, window 22:00-07:00 -> inside.
        var controller = new PetController(clock: () => new DateTime(2026, 5, 28, 23, 0, 0));
        var settings = HyperPetSettings.CreateDefault();
        settings.QuietHoursEnabled = true;
        settings.QuietHoursStart = "22:00";
        settings.QuietHoursEnd = "07:00";

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.Null(alert);
    }

    [Fact]
    public void HandleNotification_OutsideQuietHours_ShowsAlert()
    {
        // Clock fixed at 12:00, window 22:00-07:00 -> outside.
        var controller = new PetController(clock: () => new DateTime(2026, 5, 28, 12, 0, 0));
        var settings = HyperPetSettings.CreateDefault();
        settings.QuietHoursEnabled = true;
        settings.QuietHoursStart = "22:00";
        settings.QuietHoursEnd = "07:00";

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.NotNull(alert);
    }

    [Fact]
    public void HandleNotification_QuietHoursDisabled_IgnoresWindow()
    {
        // Inside the window time-wise, but the feature is off -> alert shows.
        var controller = new PetController(clock: () => new DateTime(2026, 5, 28, 23, 0, 0));
        var settings = HyperPetSettings.CreateDefault();
        settings.QuietHoursEnabled = false;
        settings.QuietHoursStart = "22:00";
        settings.QuietHoursEnd = "07:00";

        var alert = controller.HandleNotification(CreateNotification(), settings);

        Assert.NotNull(alert);
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

    private static HyperNotification CreateNotification()
    {
        return new HyperNotification(
            "discord-1",
            "Discord",
            "Friend",
            "hello from discord",
            DateTimeOffset.Parse("2026-05-14T10:00:00+07:00"),
            appUserModelId: "Discord.Discord");
    }
}

