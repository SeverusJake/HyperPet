using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;

namespace HyperPet.Core.Settings;

public sealed class HyperPetSettings
{
    public string SelectedPet { get; set; } = "miku-kimono";
    public PetBehaviorMode PetBehaviorMode { get; set; } = PetBehaviorMode.Calm;
    public int AlertDurationSeconds { get; set; } = 8;
    public bool AlertsPaused { get; set; }
    public bool ShowFullNotificationContent { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double PetLeft { get; set; } = 80;
    public double PetTop { get; set; } = 80;

    /// <summary>
    /// When true, single-clicking the speech bubble launches the source
    /// messaging app (when its AppUserModelId is known).
    /// </summary>
    public bool OpenAppOnBubbleClick { get; set; }

    /// <summary>
    /// Master toggle for the Windows Action Center polling pipeline. When
    /// true, every notification from that pipeline is considered (subject to
    /// per-app blocks in <see cref="MessagingApps"/>). When false, no Windows
    /// Action Center notification triggers the pet.
    /// </summary>
    public bool ReactToWindowsNotifications { get; set; } = true;

    /// <summary>
    /// Master toggle for the in-app popup watcher (apps like Zalo that do not
    /// route through the Windows Action Center). When true, popups from
    /// processes listed in <see cref="WatchedInAppProcesses"/> are caught.
    /// </summary>
    public bool ReactToInAppNotifications { get; set; } = true;

    /// <summary>Poll interval (seconds) for the Windows Action Center pipeline.</summary>
    public int WindowsNotificationPollIntervalSeconds { get; set; } = 30;

    /// <summary>Poll interval (seconds) for the in-app popup watcher.</summary>
    public int InAppNotificationPollIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// When true, the pet window shows a small debug overlay with the next
    /// notification poll countdown, last poll notification count, and total
    /// alerts shown this session. Also enables F1/F2/F3 sprite frame controls
    /// and number-key poll shortcuts on the pet window.
    /// </summary>
    public bool DebugMode { get; set; }

    public List<MessagingAppRule> MessagingApps { get; set; } = MessagingAppRule.CreateDefaults().ToList();

    /// <summary>
    /// Process names (no extension) whose top-level popup windows are watched
    /// as in-app notifications. Used for apps like Zalo that do not push
    /// toasts into the Windows Action Center.
    /// </summary>
    public List<string> WatchedInAppProcesses { get; set; } = new() { "Zalo" };

    public static HyperPetSettings CreateDefault() => new();
}
