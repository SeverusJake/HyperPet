namespace HyperPet.Core.Notifications;

public sealed record HyperNotification
{
    public HyperNotification(
        string sourceId,
        string appName,
        string title,
        string body,
        DateTimeOffset timestamp,
        string appUserModelId = "", NotificationSource source = NotificationSource.Windows)
    {
        SourceId = sourceId;
        AppName = appName;
        Title = title;
        Body = body;
        Timestamp = timestamp;
        AppUserModelId = appUserModelId ?? string.Empty;
        Source = source;
    }

    public string SourceId { get; init; }

    public string AppName { get; init; }

    public string Title { get; init; }

    public string Body { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string AppUserModelId { get; init; }
    public NotificationSource Source { get; init; }
}
