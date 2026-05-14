namespace HyperPet.Core.Notifications;

public sealed class NotificationDedupe
{
    private readonly HashSet<string> _seenSourceIds = new(StringComparer.Ordinal);

    public bool ShouldAlert(HyperNotification notification)
    {
        return _seenSourceIds.Add(notification.SourceId);
    }
}
