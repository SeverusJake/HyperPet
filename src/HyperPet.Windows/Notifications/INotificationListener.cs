using HyperPet.Core.Notifications;

namespace HyperPet.Windows.Notifications;

public interface INotificationListener
{
    event EventHandler<HyperNotification>? NotificationAdded;

    Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default);

    void StartListening();

    void StopListening();
}
