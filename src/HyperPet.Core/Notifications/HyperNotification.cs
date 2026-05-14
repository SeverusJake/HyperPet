namespace HyperPet.Core.Notifications;

public sealed record HyperNotification
{
    public HyperNotification(
        string sourceId,
        string appName,
        string title,
        string body,
        DateTimeOffset timestamp,
        bool canActivate)
    {
        SourceId = sourceId;
        AppName = appName;
        Title = title;
        Body = body;
        Timestamp = timestamp;
        CanActivate = canActivate;
    }

    public string SourceId { get; init; }

    public string AppName { get; init; }

    public string Title { get; init; }

    public string Body { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public bool CanActivate { get; init; }
}
