using HyperPet.Core.Settings;

namespace HyperPet.Core.Notifications;

/// <summary>
/// Adds an entry to <see cref="HyperPetSettings.MessagingApps"/> the first time
/// a notification arrives from an unknown app. This lets the user manage
/// per-app reactions from the settings UI without having to type each app's
/// name by hand.
/// </summary>
public static class AppRuleAutoDiscovery
{
    /// <summary>
    /// Inspects the notification. If no existing rule's patterns match its
    /// AppName or AppUserModelId, appends a new rule (Enabled = true) and
    /// returns true so the caller can persist settings.
    /// </summary>
    public static bool TryRegister(HyperPetSettings settings, HyperNotification notification)
    {
        if (settings is null || notification is null)
        {
            return false;
        }

        string appName = notification.AppName ?? string.Empty;
        string appUserModelId = notification.AppUserModelId ?? string.Empty;

        if (string.IsNullOrWhiteSpace(appName) && string.IsNullOrWhiteSpace(appUserModelId))
        {
            return false;
        }

        if (IsAlreadyKnown(settings.MessagingApps, appName, appUserModelId))
        {
            return false;
        }

        var patterns = BuildPatterns(appName, appUserModelId);
        if (patterns.Count == 0)
        {
            return false;
        }

        string displayName = !string.IsNullOrWhiteSpace(appName) ? appName : appUserModelId;
        settings.MessagingApps.Add(new MessagingAppRule(displayName, patterns, enabled: true));
        return true;
    }

    private static bool IsAlreadyKnown(IReadOnlyList<MessagingAppRule> rules, string appName, string appUserModelId)
    {
        foreach (var rule in rules)
        {
            foreach (var pattern in rule.MatchPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (ContainsIgnoreCase(appName, pattern) || ContainsIgnoreCase(appUserModelId, pattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<string> BuildPatterns(string appName, string appUserModelId)
    {
        var patterns = new List<string>();

        if (!string.IsNullOrWhiteSpace(appName))
        {
            patterns.Add(appName);
        }

        if (!string.IsNullOrWhiteSpace(appUserModelId)
            && !appUserModelId.Equals(appName, StringComparison.OrdinalIgnoreCase))
        {
            patterns.Add(appUserModelId);
        }

        return patterns;
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
