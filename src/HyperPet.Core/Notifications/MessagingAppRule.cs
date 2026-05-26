namespace HyperPet.Core.Notifications;

/// <summary>
/// Rule describing how to recognize a messaging app's notifications. The pet
/// only reacts to apps that match one of the configured rules when
/// <c>OnlyMessagingApps</c> is enabled in settings.
/// </summary>
public sealed class MessagingAppRule
{
    public MessagingAppRule()
    {
    }

    public MessagingAppRule(string displayName, IEnumerable<string> matchPatterns, bool enabled = true)
    {
        DisplayName = displayName;
        MatchPatterns = matchPatterns?.ToList() ?? new List<string>();
        Enabled = enabled;
    }

    /// <summary>Friendly name shown in the settings UI (e.g. "Discord").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Case-insensitive substring patterns matched against either the
    /// notification's <c>AppName</c> or its <c>AppUserModelId</c>.
    /// </summary>
    public List<string> MatchPatterns { get; set; } = new();

    public bool Enabled { get; set; } = true;

    public bool Matches(HyperNotification notification)
    {
        if (!Enabled || MatchPatterns.Count == 0)
        {
            return false;
        }

        foreach (var pattern in MatchPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (Contains(notification.AppName, pattern) || Contains(notification.AppUserModelId, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string source, string pattern)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        return source.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<MessagingAppRule> CreateDefaults()
    {
        return new[]
        {
            new MessagingAppRule("Zalo", new[] { "Zalo" }),
            new MessagingAppRule("Messenger", new[] { "Messenger", "Facebook" }),
            new MessagingAppRule("Discord", new[] { "Discord" })
        };
    }
}
