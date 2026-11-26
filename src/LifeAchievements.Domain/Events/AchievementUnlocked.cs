using LifeAchievements.Domain.Entities;

namespace LifeAchievements.Domain.Events;

/// <summary>
/// Raised when a claim is approved and the XP has been written to the ledger.
/// Handlers detect level-ups and evaluate the hidden achievement rules.
/// </summary>
public sealed record AchievementUnlocked(
    Guid UserId,
    Guid AchievementId,
    string AchievementCode,
    Guid UserAchievementId,
    int XpGained,
    int TotalXpBefore,
    int TotalXpAfter,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <summary>
    /// Filled in by the handlers: the claims they auto-unlocked while reacting to this event.
    /// The command reads it to build BonusUnlocks, which is far safer than matching on a
    /// timestamp - Postgres only keeps microseconds, so an exact equality match would miss.
    /// </summary>
    public List<Guid> BonusUserAchievementIds { get; } = [];


    public static AchievementUnlocked From(UserAchievement claim, Achievement achievement, int xpBefore, int xpAfter, DateTimeOffset at) =>
        new(claim.UserId, achievement.Id, achievement.Code, claim.Id, achievement.XpReward, xpBefore, xpAfter, at);
}
