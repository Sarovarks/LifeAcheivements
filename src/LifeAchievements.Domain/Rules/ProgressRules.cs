using LifeAchievements.Domain.Enums;

namespace LifeAchievements.Domain.Rules;

/// <summary>
/// When a progress run expires. Windows are calendar-based rather than rolling, so "five gym
/// days this week" means this week, and Monday genuinely starts fresh.
/// </summary>
public static class ProgressRules
{
    public static bool HasWindowLapsed(ProgressWindow window, DateTimeOffset startedAt, DateTimeOffset now)
    {
        if (window == ProgressWindow.Forever) return false;

        var start = startedAt.ToLocalTime();
        var current = now.ToLocalTime();

        // A clock that has gone backwards (a timezone change, say) should not wipe a run.
        if (current < start) return false;

        return window switch
        {
            ProgressWindow.Week => StartOfWeek(current.Date) > StartOfWeek(start.Date),
            ProgressWindow.Month => current.Year > start.Year || current.Month > start.Month,
            ProgressWindow.Year => current.Year > start.Year,
            _ => false
        };
    }

    /// <summary>Weeks start on Monday, which is how a "gym week" actually feels.</summary>
    public static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
        return date.Date.AddDays(-diff);
    }

    /// <summary>When the current run runs out, for the "resets in 3d" hint in the UI.</summary>
    public static DateTimeOffset? WindowEndsAt(ProgressWindow window, DateTimeOffset startedAt)
    {
        if (window == ProgressWindow.Forever) return null;

        var start = startedAt.ToLocalTime();

        var end = window switch
        {
            ProgressWindow.Week => StartOfWeek(start.Date).AddDays(7),
            ProgressWindow.Month => new DateTime(start.Year, start.Month, 1).AddMonths(1),
            ProgressWindow.Year => new DateTime(start.Year, 1, 1).AddYears(1),
            _ => start.Date
        };

        return new DateTimeOffset(end, start.Offset);
    }

    public static string Describe(ProgressWindow window) => window switch
    {
        ProgressWindow.Week => "this week",
        ProgressWindow.Month => "this month",
        ProgressWindow.Year => "this year",
        _ => "so far"
    };
}
