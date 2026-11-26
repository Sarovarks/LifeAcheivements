namespace LifeAchievements.Domain.Enums;

/// <summary>
/// How long a progress run lasts before it resets. "Five gym days" only means something
/// inside a week; "twelve books" only means something inside a year.
/// </summary>
public enum ProgressWindow
{
    /// <summary>Progress never resets - it just accumulates until the target is reached.</summary>
    Forever = 0,
    Week = 1,
    Month = 2,
    Year = 3
}
