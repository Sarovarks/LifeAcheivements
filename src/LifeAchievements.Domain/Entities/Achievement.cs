using LifeAchievements.Domain.Enums;

namespace LifeAchievements.Domain.Entities;

/// <summary>A badge the user can unlock. Catalogue data - seeded, not user created.</summary>
public class Achievement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable business key, e.g. "EARLY_BIRD". Seeding is idempotent on this.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AchievementCategory Category { get; set; }
    public AchievementTier Tier { get; set; }
    public int XpReward { get; set; }

    /// <summary>Hidden ones are masked in the list and can only be unlocked by a rule.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Shown instead of the description while a hidden achievement is locked.</summary>
    public string? Hint { get; set; }

    public bool IsRepeatable { get; set; }

    /// <summary>Minimum hours between two approved unlocks. Null = no cooldown.</summary>
    public int? CooldownHours { get; set; }

    public VerificationType VerificationType { get; set; }

    /// <summary>Icon key the frontend maps to an inline SVG.</summary>
    public string Icon { get; set; } = "trophy";

    /// <summary>
    /// Steps needed before this can be claimed, for the multi-step ones. Null means it is a
    /// single act you either did or did not do, and it stays claimable straight away.
    /// </summary>
    public int? TargetCount { get; set; }

    /// <summary>What one step is called, e.g. "gym days", "books". Plural, lowercase.</summary>
    public string? ProgressUnit { get; set; }

    /// <summary>How long a run lasts before it resets. Ignored when TargetCount is null.</summary>
    public ProgressWindow ProgressWindow { get; set; } = ProgressWindow.Forever;

    public bool HasProgress => TargetCount is > 1;

    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
