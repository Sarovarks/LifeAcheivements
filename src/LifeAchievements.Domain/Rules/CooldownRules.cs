using LifeAchievements.Domain.Enums;

namespace LifeAchievements.Domain.Rules;

/// <summary>
/// Calendar-based limits for repeatable achievements. These deliberately agree with
/// <see cref="StreakCalculator"/>, which also counts distinct local calendar days - otherwise
/// a cooldown could block a claim on a day you really did the thing and quietly break a streak.
/// </summary>
public static class CooldownRules
{
    /// <summary>True while the last claim still occupies the current day or week.</summary>
    public static bool IsStillCoolingDown(CooldownWindow window, DateTimeOffset lastApprovedAt, DateTimeOffset now)
    {
        if (window == CooldownWindow.None) return false;

        var last = lastApprovedAt.ToLocalTime();
        var current = now.ToLocalTime();

        return window switch
        {
            CooldownWindow.Day => last.Date == current.Date,
            CooldownWindow.Week => ProgressRules.StartOfWeek(last.Date) == ProgressRules.StartOfWeek(current.Date),
            _ => false
        };
    }

    /// <summary>The next midnight (or Monday) after the last claim.</summary>
    public static DateTimeOffset? NextClaimableAt(CooldownWindow window, DateTimeOffset lastApprovedAt)
    {
        if (window == CooldownWindow.None) return null;

        var last = lastApprovedAt.ToLocalTime();

        var next = window switch
        {
            CooldownWindow.Day => last.Date.AddDays(1),
            CooldownWindow.Week => ProgressRules.StartOfWeek(last.Date).AddDays(7),
            _ => last.Date
        };

        // Resolve the offset at the boundary itself so a DST change does not shift it.
        return new DateTimeOffset(next, TimeZoneInfo.Local.GetUtcOffset(next));
    }

    /// <summary>The "come back tomorrow" half of the refusal message.</summary>
    public static string Describe(CooldownWindow window) => window switch
    {
        CooldownWindow.Day => "today",
        CooldownWindow.Week => "this week",
        _ => "already"
    };

    public static string NextTime(CooldownWindow window) => window switch
    {
        CooldownWindow.Day => "Tomorrow is a new day.",
        CooldownWindow.Week => "Next week, then.",
        _ => string.Empty
    };
}
