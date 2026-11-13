using System.Security;
using HyperPet.Core.Diagnostics;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HyperPet.Windows.Notifications;

/// <summary>
/// Emits a real Windows toast under a dedicated debug AppUserModelId so that
/// HyperPet's own Windows pipeline (UserNotificationListener) catches it. Used
/// by debug key 0 to test the round trip: HyperPet -> Windows shell ->
/// notification center -> HyperPet. Debug key 9 does not come here; it opens an
/// in-process popup for the in-app watcher.
///
/// Registration is lazy: the Start Menu shortcut for the AUMI is created on
/// first use and persists across sessions (idempotent).
/// </summary>
public sealed class DebugNotificationSimulator
{
    public const string GenericAumi = "HyperPet.HyperPet.DebugGeneric";
    public const string GenericDisplayName = "HyperPet Debug Generic";

    private readonly HyperPetLogger? _logger;
    private bool _registered;

    public DebugNotificationSimulator(HyperPetLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends a real Windows toast with AppName "HyperPet Debug Generic". Uses a
    /// separate AUMI from the app itself so Windows does not suppress the
    /// banner as a self-notification.
    /// </summary>
    public void SimulateGenericNotification()
    {
        if (!_registered)
        {
            try
            {
                AumiShortcutRegistrar.EnsureRegistered(GenericAumi, GenericDisplayName);
                _registered = true;
                _logger?.Info($"Registered debug AUMI '{GenericAumi}' as '{GenericDisplayName}'");
            }
            catch (Exception exception)
            {
                _logger?.Error($"Could not register debug AUMI '{GenericAumi}'", exception);
                throw;
            }
        }

        XmlDocument document = new();
        document.LoadXml(BuildToastXml("System update", "Updates are ready to install."));
        ToastNotificationManager.CreateToastNotifier(GenericAumi).Show(new ToastNotification(document));

        _logger?.Info($"Debug toast emitted aumi='{GenericAumi}'");
    }

    internal static string BuildToastXml(string title, string body) =>
        "<toast><visual><binding template=\"ToastGeneric\">" +
        $"<text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(body)}</text>" +
        "</binding></visual></toast>";
}
