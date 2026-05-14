using HyperPet.Core.Notifications;

namespace HyperPet.Windows.Notifications;

public interface INotificationListener
{
    Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default);

    Task<bool> TryActivateAsync(HyperNotification notification, CancellationToken cancellationToken = default);
}
