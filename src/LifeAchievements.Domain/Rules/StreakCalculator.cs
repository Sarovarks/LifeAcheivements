namespace LifeAchievements.Domain.Rules;

/// <summary>
/// Streak = consecutive days with at least one approved unlock, counting back from today.
/// Computed, never stored (v1). Yesterday still counts so the streak does not die at midnight.
/// </summary>
public static class StreakCalculator
{
    public static int Calculate(IEnumerable<DateTimeOffset> approvedAtTimes, DateTimeOffset now)
    {
        // Timestamps are stored as UTC; "a day" only means anything in the player's own time.
        var days = approvedAtTimes
            .Select(t => t.ToLocalTime().Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (days.Count == 0) return 0;

        var today = now.ToLocalTime().Date;
        var cursor = days[0] == today ? today
            : days[0] == today.AddDays(-1) ? today.AddDays(-1)
            : (DateTime?)null;

        if (cursor is null) return 0;

        var streak = 0;
        foreach (var day in days)
        {
            if (day == cursor)
            {
                streak++;
                cursor = cursor.Value.AddDays(-1);
            }
            else if (day < cursor)
            {
                break;
            }
        }

        return streak;
    }
}
