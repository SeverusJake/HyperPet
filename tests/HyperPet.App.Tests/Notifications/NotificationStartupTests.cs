using HyperPet.App.Notifications;
using HyperPet.Windows.Notifications;

namespace HyperPet.App.Tests.Notifications;

public sealed class NotificationStartupTests
{
    [Fact]
    public void CreationFailure_IsReturnedWithoutEscaping_AndNextAttemptCanRetry()
    {
        int attempts = 0;
        INotificationListener Create() { attempts++; throw new InvalidOperationException("native unavailable"); }
        Assert.False(NotificationListenerFactory.TryCreate(Create, out var listener, out var error));
        Assert.Null(listener);
        Assert.IsType<InvalidOperationException>(error);
        Assert.False(NotificationListenerFactory.TryCreate(Create, out _, out _));
        Assert.Equal(2, attempts);
    }
}
