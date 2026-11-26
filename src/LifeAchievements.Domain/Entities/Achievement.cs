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

    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
