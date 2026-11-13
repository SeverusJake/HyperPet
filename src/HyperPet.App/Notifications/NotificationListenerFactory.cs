using HyperPet.Windows.Notifications;

namespace HyperPet.App.Notifications;

internal static class NotificationListenerFactory
{
    public static bool TryCreate(Func<INotificationListener> create,
        out INotificationListener? listener, out Exception? error)
    {
        try { listener = create(); error = null; return true; }
        catch (Exception exception) { listener = null; error = exception; return false; }
    }
}
