using LifeAchievements.Domain.Enums;

namespace LifeAchievements.Domain.Entities;

/// <summary>One claim of one achievement by one user. Repeatables get one row per unlock.</summary>
public class UserAchievement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public Achievement Achievement { get; set; } = null!;

    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;
    public DateTimeOffset ClaimedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Friend who verified the claim (Phase 2).</summary>
    public Guid? VerifiedByUserId { get; set; }

    /// <summary>
    /// What actually happened, in the player's own words. Optional, added on the unlock screen.
    /// This is what makes the app worth reopening in a year - the XP is just the score.
    /// </summary>
    public string? Note { get; set; }

    public const int MaxNoteLength = 280;

    public void Approve(DateTimeOffset at, Guid? verifiedBy = null)
    {
        Status = ClaimStatus.Approved;
        ApprovedAt = at;
        VerifiedByUserId = verifiedBy;
    }
}
