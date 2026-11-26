using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Events;
using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Application.Achievements;

public record ApprovalOutcome(
    int XpBefore,
    int XpAfter,
    int OldLevel,
    int NewLevel,
    bool LeveledUp,
    IReadOnlyList<Guid> BonusUserAchievementIds);

public interface IUnlockService
{
    Task<ApprovalOutcome> ApproveAsync(
        UserAchievement claim,
        Achievement achievement,
        AppUser user,
        AchievementProgress? run,
        DateTimeOffset now,
        Guid? verifiedBy,
        bool isNewClaim,
        CancellationToken ct);
}

/// <summary>
/// The one place a claim turns into XP. Both paths go through here - claiming a self-verified
/// achievement, and a friend verifying a pending one - so the ledger, the cached total, the
/// hidden-achievement rules and the level-up all behave identically whoever triggered it.
/// </summary>
public class UnlockService(IAppDbContext db, IDomainEventDispatcher events) : IUnlockService
{
    public async Task<ApprovalOutcome> ApproveAsync(
        UserAchievement claim,
        Achievement achievement,
        AppUser user,
        AchievementProgress? run,
        DateTimeOffset now,
        Guid? verifiedBy,
        bool isNewClaim,
        CancellationToken ct)
    {
        var xpBefore = user.TotalXp;
        var oldLevel = LevelCurve.LevelFor(xpBefore);

        await using var tx = await db.BeginTransactionAsync(ct);

        claim.Approve(now, verifiedBy);
        if (isNewClaim) db.UserAchievements.Add(claim);

        db.XpTransactions.Add(new XpTransaction
        {
            UserId = claim.UserId,
            Amount = achievement.XpReward,
            Reason = $"Unlocked {achievement.Name}",
            SourceUserAchievementId = claim.Id,
            CreatedAt = now
        });

        user.TotalXp = xpBefore + achievement.XpReward;

        // A finished run has been cashed in; the next one starts from zero.
        if (run is not null)
        {
            run.Current = 0;
            run.WindowStartedAt = now;
            run.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        // Handlers evaluate the hidden rules and may add more ledger rows of their own.
        var unlocked = AchievementUnlocked.From(claim, achievement, xpBefore, user.TotalXp, now);
        await events.Dispatch(unlocked, ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        var newLevel = LevelCurve.LevelFor(user.TotalXp);

        return new ApprovalOutcome(
            xpBefore,
            user.TotalXp,
            oldLevel,
            newLevel,
            LeveledUp: newLevel > oldLevel,
            unlocked.BonusUserAchievementIds);
    }
}
