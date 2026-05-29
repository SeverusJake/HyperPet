using HyperPet.Core.Diagnostics;
using HyperPet.Core.Notifications;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace HyperPet.Windows.Notifications;

public sealed class WindowsNotificationListener : INotificationListener
{
    private readonly UserNotificationListener listener;
    private readonly HyperPetLogger? logger;
    private TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs>? changedHandler;
    private bool isListening;

    // Retains every WinRT UserNotification returned to us. Prevents the GC
    // finalizer thread from releasing the underlying COM objects, which causes
    // CFG violations (process crash) on unpackaged desktop apps due to a
    // wpnapps.dll lifetime bug. Memory cost: ~1 ref per unique notification
    // for the lifetime of the app session.
    private readonly List<UserNotification> retainedNotifications = new();
    private const int MaxRetained = 256;

    public event EventHandler<HyperNotification>? NotificationAdded;

    public WindowsNotificationListener(HyperPetLogger? logger = null)
        : this(UserNotificationListener.Current, logger)
    {
    }

    private WindowsNotificationListener(UserNotificationListener listener, HyperPetLogger? logger)
    {
        this.listener = listener;
        this.logger = logger;
    }

    public async Task<NotificationAccessStatus> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UserNotificationListenerAccessStatus status = await listener.RequestAccessAsync().AsTask(cancellationToken);

        return status switch
        {
            UserNotificationListenerAccessStatus.Allowed => NotificationAccessStatus.Allowed,
            UserNotificationListenerAccessStatus.Denied => NotificationAccessStatus.Denied,
            _ => NotificationAccessStatus.Unspecified
        };
    }

    public void StartListening()
    {
        if (isListening)
        {
            return;
        }

        try
        {
            changedHandler = OnNotificationChanged;
            listener.NotificationChanged += changedHandler;
            isListening = true;
            logger?.Info("NotificationChanged event subscribed");
        }
        catch (Exception exception)
        {
            // NotificationChanged event may require package identity (MSIX). If
            // subscription fails on unpackaged desktop, fall back to polling only.
            logger?.Warn("Could not subscribe to NotificationChanged event", exception);
            changedHandler = null;
            isListening = false;
        }
    }

    public void StopListening()
    {
        if (!isListening || changedHandler is null)
        {
            return;
        }

        try
        {
            listener.NotificationChanged -= changedHandler;
        }
        catch (Exception exception)
        {
            logger?.Warn("Could not unsubscribe from NotificationChanged event", exception);
        }
        finally
        {
            changedHandler = null;
            isListening = false;
        }
    }

    private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        if (args.ChangeKind != UserNotificationChangedKind.Added)
        {
            return;
        }

        try
        {
            UserNotification? notification = sender.GetNotification(args.UserNotificationId);
            if (notification is null)
            {
                return;
            }

            HyperNotification? hyper = TryConvert(notification);
            if (hyper is not null)
            {
                lock (retainedNotifications)
                {
                    retainedNotifications.Add(notification);
                    TrimRetained();
                }
                NotificationAdded?.Invoke(this, hyper);
            }
        }
        catch (Exception exception)
        {
            logger?.Warn($"Could not handle NotificationChanged for id={args.UserNotificationId}", exception);
        }
    }

    public async Task<IReadOnlyList<HyperNotification>> GetActiveNotificationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<UserNotification> notifications = await listener
            .GetNotificationsAsync(NotificationKinds.Toast)
            .AsTask(cancellationToken);

        List<HyperNotification> hyperNotifications = new(notifications.Count);

        foreach (UserNotification notification in notifications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HyperNotification? hyper = TryConvert(notification);
            if (hyper is not null)
            {
                hyperNotifications.Add(hyper);
            }
        }

        // Retain the WinRT collection and every UserNotification so their COM
        // refs are never released by the GC finalizer (which crashes due to
        // the wpnapps.dll bug on unpackaged desktop). The cost is a small
        // permanent memory hold for the app session.
        lock (retainedNotifications)
        {
            retainedNotifications.AddRange(notifications);
            TrimRetained();
        }

        return hyperNotifications;
    }

    public Task<bool> TryActivateAsync(HyperNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(false);
    }

    private void TrimRetained()
    {
        // Caller already holds the lock on retainedNotifications.
        if (retainedNotifications.Count > MaxRetained)
        {
            retainedNotifications.RemoveRange(0, retainedNotifications.Count - MaxRetained);
        }
    }

    private HyperNotification? TryConvert(UserNotification notification)
    {
        try
        {
            string appUserModelId = notification.AppInfo.AppUserModelId ?? string.Empty;
            string appName = notification.AppInfo.DisplayInfo.DisplayName;
            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = string.IsNullOrWhiteSpace(appUserModelId) ? "Unknown app" : appUserModelId;
            }

            IReadOnlyList<string> textElements = ExtractToastGenericText(notification.Notification);

            string title = textElements.Count > 0 ? textElements[0] : "Notification";
            string body = textElements.Count > 1
                ? string.Join(Environment.NewLine, textElements.Skip(1))
                : string.Empty;
            string sourceId = string.Join(
                ':',
                appUserModelId,
                notification.Id.ToString(),
                notification.CreationTime.ToUnixTimeMilliseconds().ToString());

            return new HyperNotification(
                sourceId,
                appName,
                title,
                body,
                notification.CreationTime,
                canActivate: false,
                appUserModelId: appUserModelId);
        }
        catch (Exception exception)
        {
            logger?.Warn($"Could not parse Windows notification {notification.Id}", exception);
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractToastGenericText(Notification notification)
    {
        NotificationBinding? toastGenericBinding = notification.Visual.Bindings
            .FirstOrDefault(binding => binding.Template == KnownNotificationBindings.ToastGeneric);

        if (toastGenericBinding is null)
        {
            return Array.Empty<string>();
        }

        return toastGenericBinding
            .GetTextElements()
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    }
}
