using LifeAchievements.Domain.Enums;

namespace LifeAchievements.Domain.Entities;

/// <summary>
/// A cheer or a nod of respect on somebody's unlock. One row per person per unlock per type,
/// so the same person can both cheer and respect, and each toggles on its own.
/// </summary>
public class Reaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAchievementId { get; set; }
    public UserAchievement UserAchievement { get; set; } = null!;

    /// <summary>Who reacted - not the person who earned it.</summary>
    public Guid UserId { get; set; }

    public ReactionType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
