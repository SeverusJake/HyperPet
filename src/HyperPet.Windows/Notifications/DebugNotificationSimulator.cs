using HyperPet.Core.Diagnostics;
using CommunityToolkit.WinUI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HyperPet.Windows.Notifications;

/// <summary>
/// Emits real Windows toast notifications under controlled AppUserModelIds so
/// that HyperPet's own notification pipeline (UserNotificationListener poll)
/// catches them. Used by the debug overlay's number-key shortcuts to test the
/// full round-trip: HyperPet -> Windows shell -> Action Center -> HyperPet.
///
/// Two registered AUMIs:
///  - HyperPet (default, generic notification) -> press 0
///  - HyperPet Debug Messenger (looks like a messaging app) -> press 9
///
/// Registration is lazy: the Start Menu shortcut for each AUMI is created on
/// first use and persists across sessions (idempotent).
/// </summary>
public sealed class DebugNotificationSimulator
{
    public const string GenericAumi = "HyperPet.HyperPet.DebugGeneric";
    public const string GenericDisplayName = "HyperPet Debug Generic";

    public const string MessengerAumi = "HyperPet.HyperPet.DebugMessenger";
    public const string MessengerDisplayName = "HyperPet Debug Messenger";

    private readonly HyperPetLogger? _logger;
    private bool _registeredGeneric;
    private bool _registeredMessenger;

    public DebugNotificationSimulator(HyperPetLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends a real Windows toast under the messaging-app AUMI. The notification
    /// appears in Action Center with AppName "HyperPet Debug Messenger", so the
    /// "React to messaging apps" filter must include that name (or be off) for
    /// HyperPet to react to it.
    /// </summary>
    public void SimulateMessagingNotification()
    {
        EnsureRegistered(MessengerAumi, MessengerDisplayName, ref _registeredMessenger);

        Show(
            aumi: MessengerAumi,
            title: "John Doe",
            body: "Hey, are you free for a quick chat?");
    }

    /// <summary>
    /// Sends a real Windows toast under a generic (non-messaging) debug AUMI.
    /// The notification appears in Action Center with AppName
    /// "HyperPet Debug Generic". With the messaging filter on, this is filtered
    /// out (proves the filter works). Uses a separate AUMI from the app itself
    /// so Windows does not suppress the banner as a self-notification.
    /// </summary>
    public void SimulateGenericNotification()
    {
        EnsureRegistered(GenericAumi, GenericDisplayName, ref _registeredGeneric);

        Show(
            aumi: GenericAumi,
            title: "System update",
            body: "Updates are ready to install.");
    }

    private void EnsureRegistered(string aumi, string displayName, ref bool registeredFlag)
    {
        if (registeredFlag)
        {
            return;
        }

        try
        {
            AumiShortcutRegistrar.EnsureRegistered(aumi, displayName);
            registeredFlag = true;
            _logger?.Info($"Registered debug AUMI '{aumi}' as '{displayName}'");
        }
        catch (Exception exception)
        {
            _logger?.Error($"Could not register debug AUMI '{aumi}'", exception);
            throw;
        }
    }

    private void Show(string aumi, string title, string body)
    {
        ToastContent content = new ToastContentBuilder()
            .AddText(title)
            .AddText(body)
            .GetToastContent();

        XmlDocument document = new();
        document.LoadXml(content.GetContent());

        ToastNotification toast = new(document);
        ToastNotificationManager.CreateToastNotifier(aumi).Show(toast);

        _logger?.Info($"Debug toast emitted aumi='{aumi}' title='{title}'");
    }
}
