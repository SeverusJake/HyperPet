using System.Diagnostics;
using HyperPet.Core.Notifications;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace HyperPet.Windows.Notifications;

public sealed class WindowsNotificationListener : INotificationListener
{
    private readonly UserNotificationListener listener;

    public WindowsNotificationListener()
        : this(UserNotificationListener.Current)
    {
    }

    private WindowsNotificationListener(UserNotificationListener listener)
    {
        this.listener = listener;
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

                hyperNotifications.Add(new HyperNotification(
                    sourceId,
                    appName,
                    title,
                    body,
                    notification.CreationTime,
                    canActivate: false));
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Could not parse Windows notification {notification.Id}: {exception}");
            }
        }

        return hyperNotifications;
    }

    public Task<bool> TryActivateAsync(HyperNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(false);
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
