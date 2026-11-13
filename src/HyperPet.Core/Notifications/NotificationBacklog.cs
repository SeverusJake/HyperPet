namespace HyperPet.Core.Notifications;

/// <summary>
/// Windows keeps delivered toasts in the notification center, so the first
/// poll after monitoring starts returns messages the user already received.
/// Anything created before monitoring began is backlog and is never shown.
/// In-app popups are observed live and are never backlog.
/// </summary>
public sealed class NotificationBacklog
{
    private readonly TimeProvider _time;
    private DateTimeOffset _since;

    public NotificationBacklog(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _since = _time.GetUtcNow();
    }

    /// <summary>Call whenever Windows monitoring (re)starts.</summary>
    public void Reset() => _since = _time.GetUtcNow();

    public bool IsBacklog(HyperNotification notification) =>
        notification.Source == NotificationSource.Windows && notification.Timestamp < _since;
}
