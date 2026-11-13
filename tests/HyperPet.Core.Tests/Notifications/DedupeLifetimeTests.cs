using HyperPet.Core.Notifications;

namespace HyperPet.Core.Tests.Notifications;

public sealed class DedupeLifetimeTests
{
    [Fact]
    public void CapacityEviction_AllowsAnOldNotificationAgain()
    {
        var dedupe = new NotificationDedupe(capacity: 2);
        Assert.True(dedupe.ShouldAlert(Notification("a")));
        Assert.False(dedupe.ShouldAlert(Notification("a")));
        dedupe.ShouldAlert(Notification("b")); dedupe.ShouldAlert(Notification("c"));
        Assert.True(dedupe.ShouldAlert(Notification("a")));
    }
    [Fact]
    public void Expiry_AllowsAnOldNotificationAgain()
    {
        var time = new Clock(); var dedupe = new NotificationDedupe(time, ttl: TimeSpan.FromMinutes(1));
        Assert.True(dedupe.ShouldAlert(Notification("a")));
        time.Now += TimeSpan.FromMinutes(2);
        Assert.True(dedupe.ShouldAlert(Notification("a")));
    }
    private static HyperNotification Notification(string id) => new(id, "App", "Title", "Body", DateTimeOffset.UtcNow);
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
