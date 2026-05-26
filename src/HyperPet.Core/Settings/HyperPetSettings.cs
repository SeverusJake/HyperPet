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
    /// When true, the pet only reacts to notifications from apps matching one
    /// of the rules in <see cref="MessagingApps"/>.
    /// </summary>
    public bool OnlyMessagingApps { get; set; } = true;

    /// <summary>
    /// When true, single-clicking the speech bubble launches the source
    /// messaging app (when its AppUserModelId is known).
    /// </summary>
    public bool OpenAppOnBubbleClick { get; set; }

    /// <summary>
    /// Master toggle for the Windows notification listener. When false, the
    /// app stops polling and stops dispatching pet alerts. Useful for going
    /// "silent" without quitting the app.
    /// </summary>
    public bool ReactToWindowsNotifications { get; set; } = true;

    /// <summary>
    /// When true, the pet window shows a small debug overlay with the next
    /// notification poll countdown, last poll notification count, and total
    /// alerts shown this session. Also enables F1/F2/F3 sprite frame controls
    /// and number-key poll shortcuts on the pet window.
    /// </summary>
    public bool DebugMode { get; set; }

    public List<MessagingAppRule> MessagingApps { get; set; } = MessagingAppRule.CreateDefaults().ToList();

    public static HyperPetSettings CreateDefault() => new();
}
