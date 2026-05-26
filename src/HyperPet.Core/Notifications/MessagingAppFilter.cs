namespace HyperPet.Core.Notifications;

public sealed class MessagingAppFilter
{
    private readonly IReadOnlyList<MessagingAppRule> _rules;

    public MessagingAppFilter(IEnumerable<MessagingAppRule> rules)
    {
        _rules = rules?.ToList() ?? new List<MessagingAppRule>();
    }

    public bool IsMessagingApp(HyperNotification notification)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(notification))
            {
                return true;
            }
        }

        return false;
    }

    public MessagingAppRule? FindMatch(HyperNotification notification)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(notification))
            {
                return rule;
            }
        }

        return null;
    }
}
