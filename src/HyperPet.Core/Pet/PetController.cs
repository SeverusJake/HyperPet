using HyperPet.Core.Notifications;
using HyperPet.Core.Settings;

namespace HyperPet.Core.Pet;

public sealed class PetController
{
    private readonly MessagingAppFilter? _messagingAppFilter;

    public PetController()
        : this(messagingAppFilter: null)
    {
    }

    public PetController(MessagingAppFilter? messagingAppFilter)
    {
        _messagingAppFilter = messagingAppFilter;
    }

    public PetState State { get; private set; } = PetState.Idle;

    public PetAlert? CurrentAlert { get; private set; }

    public PetAlert? HandleNotification(HyperNotification notification, HyperPetSettings settings)
    {
        if (settings.AlertsPaused)
        {
            return null;
        }

        if (settings.OnlyMessagingApps)
        {
            var filter = _messagingAppFilter ?? new MessagingAppFilter(settings.MessagingApps);
            if (!filter.IsMessagingApp(notification))
            {
                return null;
            }
        }

        var title = settings.ShowFullNotificationContent ? notification.Title : "Notification";
        var body = settings.ShowFullNotificationContent ? notification.Body : string.Empty;

        var alert = new PetAlert(
            notification.AppName,
            title,
            body,
            notification.Timestamp,
            notification.CanActivate,
            notification.AppUserModelId);

        CurrentAlert = alert;
        State = PetState.Alerting;

        return alert;
    }

    public void DismissAlert()
    {
        CurrentAlert = null;
        State = PetState.Idle;
    }
}
