using HyperPet.Core.Notifications;

namespace HyperPet.Core.Tests.Notifications;

public sealed class MessagingAppFilterTests
{
    [Fact]
    public void IsMessagingApp_ReturnsTrue_WhenAppNameContainsPattern()
    {
        var filter = new MessagingAppFilter(MessagingAppRule.CreateDefaults());
        var notification = new HyperNotification(
            "discord-1",
            "Discord",
            "Friend",
            "hi",
            DateTimeOffset.UtcNow,
            canActivate: false);

        Assert.True(filter.IsMessagingApp(notification));
    }

    [Fact]
    public void IsMessagingApp_ReturnsTrue_WhenAumidContainsPattern()
    {
        var filter = new MessagingAppFilter(MessagingAppRule.CreateDefaults());
        var notification = new HyperNotification(
            "messenger-1",
            "Some App",
            "Friend",
            "hi",
            DateTimeOffset.UtcNow,
            canActivate: false,
            appUserModelId: "Facebook.317180B0BB486_8xx8rvfyw5nnt!App");

        Assert.True(filter.IsMessagingApp(notification));
    }

    [Fact]
    public void IsMessagingApp_ReturnsFalse_ForNonMatchingApp()
    {
        var filter = new MessagingAppFilter(MessagingAppRule.CreateDefaults());
        var notification = new HyperNotification(
            "outlook-1",
            "Outlook",
            "Boss",
            "ping",
            DateTimeOffset.UtcNow,
            canActivate: false,
            appUserModelId: "Microsoft.Office.OUTLOOK");

        Assert.False(filter.IsMessagingApp(notification));
    }

    [Fact]
    public void IsMessagingApp_ReturnsFalse_WhenRuleDisabled()
    {
        var rules = MessagingAppRule.CreateDefaults().ToList();
        foreach (var rule in rules)
        {
            rule.Enabled = false;
        }

        var filter = new MessagingAppFilter(rules);
        var notification = new HyperNotification(
            "discord-1",
            "Discord",
            "Friend",
            "hi",
            DateTimeOffset.UtcNow,
            canActivate: false);

        Assert.False(filter.IsMessagingApp(notification));
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var rule = new MessagingAppRule("Zalo", new[] { "zalo" });
        var notification = new HyperNotification(
            "zalo-1",
            "ZALO Desktop",
            "Bạn",
            "Chào",
            DateTimeOffset.UtcNow,
            canActivate: false);

        Assert.True(rule.Matches(notification));
    }
}
