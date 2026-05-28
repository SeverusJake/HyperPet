using System.Globalization;

namespace HyperPet.Core.Settings;

/// <summary>
/// Decides whether a given time falls inside a daily quiet-hours window.
/// The window may wrap past midnight (e.g. 22:00 to 07:00).
/// </summary>
public static class QuietHoursSchedule
{
    /// <summary>
    /// Parses an "HH:mm" 24-hour string into a <see cref="TimeOnly"/>.
    /// Returns null when the input is null, blank, or malformed.
    /// </summary>
    public static TimeOnly? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TimeOnly.TryParseExact(text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// True when <paramref name="now"/> is within [start, end). When start
    /// equals end the window is empty (always false). When start is after end
    /// the window wraps midnight.
    /// </summary>
    public static bool IsActive(TimeOnly now, TimeOnly start, TimeOnly end)
    {
        if (start == end)
        {
            return false;
        }

        if (start < end)
        {
            return now >= start && now < end;
        }

        // Overnight wrap: active from start to midnight, then midnight to end.
        return now >= start || now < end;
    }

    /// <summary>
    /// Convenience overload that parses the stored "HH:mm" strings. Returns
    /// false if either string fails to parse (treat misconfigured windows as
    /// inactive rather than silently suppressing everything).
    /// </summary>
    public static bool IsActive(TimeOnly now, string? start, string? end)
    {
        TimeOnly? s = TryParse(start);
        TimeOnly? e = TryParse(end);
        if (s is null || e is null)
        {
            return false;
        }

        return IsActive(now, s.Value, e.Value);
    }
}
