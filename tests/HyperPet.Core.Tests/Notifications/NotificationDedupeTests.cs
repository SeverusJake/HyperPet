using HyperPet.Core.Notifications;

namespace HyperPet.Core.Tests.Notifications;

public sealed class NotificationDedupeTests
{
    [Fact]
    public void ShouldAlert_FirstTime_ReturnsTrue()
    {
        var dedupe = new NotificationDedupe();
        var notification = CreateNotification("discord-1");

        Assert.True(dedupe.ShouldAlert(notification));
    }

    [Fact]
    public void ShouldAlert_SameNotificationTwice_ReturnsFalseSecondTime()
    {
        var dedupe = new NotificationDedupe();
        var notification = CreateNotification("discord-1");

        Assert.True(dedupe.ShouldAlert(notification));
        Assert.False(dedupe.ShouldAlert(notification));
    }

    [Fact]
    public void ShouldAlert_SameContentDifferentSourceId_ReturnsTrue()
    {
        var dedupe = new NotificationDedupe();

        Assert.True(dedupe.ShouldAlert(CreateNotification("discord-1")));
        Assert.True(dedupe.ShouldAlert(CreateNotification("discord-2")));
    }

    private static HyperNotification CreateNotification(string id)
    {
        return new HyperNotification(
            id,
            "Discord",
            "Friend",
            "hello",
            DateTimeOffset.Parse("2026-05-14T10:00:00+07:00"),
            canActivate: true);
    }
}
