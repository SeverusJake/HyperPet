using HyperPet.Core.Notifications;
using HyperPet.Core.Settings;

namespace HyperPet.Core.Pet;

public sealed class PetController
{
    private readonly MessagingAppFilter? _messagingAppFilter;
    private readonly Func<DateTime> _clock;

    public PetController()
        : this(messagingAppFilter: null)
    {
    }

    public PetController(MessagingAppFilter? messagingAppFilter, Func<DateTime>? clock = null)
    {
        _messagingAppFilter = messagingAppFilter;
        _clock = clock ?? (() => DateTime.Now);
    }

    public PetState State { get; private set; } = PetState.Idle;

    public PetAlert? CurrentAlert { get; private set; }

    public PetAlert? HandleNotification(HyperNotification notification, HyperPetSettings settings)
    {
        if (settings.AlertsPaused)
        {
            return null;
        }

        // Scheduled quiet hours: suppress while inside the configured window.
        if (settings.QuietHoursEnabled
            && QuietHoursSchedule.IsActive(
                TimeOnly.FromDateTime(_clock()),
                settings.QuietHoursStart,
                settings.QuietHoursEnd))
        {
            return null;
        }

        // Per-app block: if a rule's patterns match the notification AND the
        // rule's Enabled flag is off, suppress. Source-level master gates
        // (ReactToWindowsNotifications / ReactToInAppNotifications) are
        // applied upstream by the dispatcher.
        var matchedRule = FindMatchingRule(settings.MessagingApps, notification);
        if (matchedRule is not null && !matchedRule.Enabled)
        {
            return null;
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

    private static MessagingAppRule? FindMatchingRule(IEnumerable<MessagingAppRule>? rules, HyperNotification notification)
    {
        if (rules is null)
        {
            return null;
        }

        foreach (var rule in rules)
        {
            if (rule is null)
            {
                continue;
            }

            foreach (var pattern in rule.MatchPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (ContainsIgnoreCase(notification.AppName, pattern)
                    || ContainsIgnoreCase(notification.AppUserModelId, pattern))
                {
                    return rule;
                }
            }
        }

        return null;
    }

    private static bool ContainsIgnoreCase(string source, string pattern)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        return source.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
