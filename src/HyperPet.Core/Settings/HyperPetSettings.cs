using HyperPet.Core.Notifications;
using HyperPet.Core.Pets;

namespace HyperPet.Core.Settings;

public sealed class HyperPetSettings
{
    public string SelectedPet { get; set; } = "miku-kimono";
    public PetBehaviorMode PetBehaviorMode { get; set; } = PetBehaviorMode.Calm;
    public int AlertDurationSeconds { get; set; } = 8;

    /// <summary>
    /// Pet display size on a 1-10 scale. 8 is the design baseline (100%).
    /// Lower values shrink the sprite, higher values enlarge it.
    /// </summary>
    public int PetSize { get; set; } = 8;

    /// <summary>
    /// Horizontal move speed (pixels per movement tick) while the pet is in
    /// Running behavior mode. 1-20, default 2.
    /// </summary>
    public int RunningSpeed { get; set; } = 2;
    public bool AlertsPaused { get; set; }
    public bool ShowFullNotificationContent { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double PetLeft { get; set; } = 80;
    public double PetTop { get; set; } = 80;

    /// <summary>
    /// When true, single-clicking the speech bubble launches the source
    /// messaging app (when its AppUserModelId is known).
    /// </summary>
    public bool OpenAppOnBubbleClick { get; set; } = true;

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

    /// <summary>
    /// When true, HyperPet checks GitHub for a newer release once at launch
    /// and prompts to install it. Manual checks via the About tab work
    /// regardless of this flag. Off by default.
    /// </summary>
    public bool AutoUpdate { get; set; } = false;

    /// <summary>
    /// When true, alerts are suppressed during the daily window between
    /// <see cref="QuietHoursStart"/> and <see cref="QuietHoursEnd"/> (which may
    /// wrap past midnight). Independent of manual <see cref="AlertsPaused"/>.
    /// </summary>
    public bool QuietHoursEnabled { get; set; }

    /// <summary>Quiet-hours window start as 24-hour "HH:mm". Default "22:00".</summary>
    public string QuietHoursStart { get; set; } = "22:00";

    /// <summary>Quiet-hours window end as 24-hour "HH:mm". Default "07:00".</summary>
    public string QuietHoursEnd { get; set; } = "07:00";

    public List<MessagingAppRule> MessagingApps { get; set; } = MessagingAppRule.CreateDefaults().ToList();

    /// <summary>
    /// Process names (no extension) whose top-level popup windows are watched
    /// as in-app notifications. Used for apps like Zalo that do not push
    /// toasts into the Windows Action Center.
    /// </summary>
    public List<string> WatchedInAppProcesses { get; set; } = new() { "Zalo" };

    /// <summary>
    /// Per-pet, per-state animation FPS overrides. Outer key is pet id (e.g.
    /// "miku-kimono"), inner key is the state name as it appears in pet.json
    /// (e.g. "idle", "runRight"). When a state is not present in the inner
    /// dictionary, the pet.json default fps is used.
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> StateSpeedOverrides { get; set; } = new();

    /// <summary>
    /// Per-pet, per-state animation play-mode overrides (Forward / Reverse /
    /// PingPong). Same shape as <see cref="StateSpeedOverrides"/>: outer key
    /// is pet id, inner key is the state name from pet.json. Missing entries
    /// fall back to the pet.json playMode (or Forward when unspecified).
    /// </summary>
    public Dictionary<string, Dictionary<string, PlayMode>> StatePlayModeOverrides { get; set; } = new();

    public static HyperPetSettings CreateDefault() => new();
}
