namespace HyperPet.Core.Notifications;

/// <summary>
/// Rule describing how to recognize an app's notifications. When a rule's
/// patterns match a notification and the rule is disabled, the pet suppresses
/// that app's alerts (per-app block, applied in PetController).
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
