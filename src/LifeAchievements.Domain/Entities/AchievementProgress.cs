using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Domain.Entities;

/// <summary>
/// How far one user has got towards one multi-step achievement. This is what turns
/// "gym five days in a week" from a single honour-system tap into something you work at.
/// One row per user per achievement; the run resets when its window lapses.
/// </summary>
public class AchievementProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public Achievement Achievement { get; set; } = null!;

    /// <summary>Steps logged in the current run.</summary>
    public int Current { get; set; }

    /// <summary>When the current run started - the window is measured from here.</summary>
    public DateTimeOffset WindowStartedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Drops a stale run so a half-finished week does not linger forever.
    /// Returns true if the run was reset.
    /// </summary>
    public bool ResetIfWindowLapsed(ProgressWindow window, DateTimeOffset now)
    {
        if (!ProgressRules.HasWindowLapsed(window, WindowStartedAt, now)) return false;

        Current = 0;
        WindowStartedAt = now;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Adds steps, never going past the target or below zero.</summary>
    public void Log(int amount, int target, DateTimeOffset now)
    {
        if (Current == 0) WindowStartedAt = now;

        Current = Math.Clamp(Current + amount, 0, target);
        UpdatedAt = now;
    }

    public bool IsComplete(int target) => Current >= target;
}
