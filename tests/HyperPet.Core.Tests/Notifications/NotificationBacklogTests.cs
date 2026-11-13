using HyperPet.Core.Notifications;

namespace HyperPet.Core.Tests.Notifications;

public sealed class NotificationBacklogTests
{
    [Fact]
    public void WindowsNotificationCreatedBeforeStart_IsBacklog_LaterOneIsNot()
    {
        var time = new Clock();
        var backlog = new NotificationBacklog(time);

        Assert.True(backlog.IsBacklog(Notification(time.Now.AddMinutes(-1), NotificationSource.Windows)));
        Assert.False(backlog.IsBacklog(Notification(time.Now.AddSeconds(1), NotificationSource.Windows)));
    }

    [Fact]
    public void Reset_MovesTheBoundaryToNow()
    {
        var time = new Clock();
        var backlog = new NotificationBacklog(time);
        var arrivedWhileOff = Notification(time.Now.AddMinutes(5), NotificationSource.Windows);
        Assert.False(backlog.IsBacklog(arrivedWhileOff));

        time.Now += TimeSpan.FromMinutes(10);
        backlog.Reset();

        Assert.True(backlog.IsBacklog(arrivedWhileOff));
    }

    [Fact]
    public void InAppPopup_IsNeverBacklog()
    {
        var time = new Clock();
        var backlog = new NotificationBacklog(time);

        Assert.False(backlog.IsBacklog(Notification(time.Now.AddHours(-1), NotificationSource.InApp)));
    }

    private static HyperNotification Notification(DateTimeOffset created, NotificationSource source) =>
        new("id", "App", "Title", "Body", created, source: source);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-13T09:00:00+07:00");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
