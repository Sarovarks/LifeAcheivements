using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Events;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Achievements.Commands;

public record ClaimAchievementCommand(Guid AchievementId) : ICommand<UnlockResultDto>;

public class ClaimAchievementCommandValidator : AbstractValidator<ClaimAchievementCommand>
{
    public ClaimAchievementCommandValidator()
    {
        RuleFor(x => x.AchievementId).NotEmpty().WithMessage("Which achievement? No id was given.");
    }
}

public class ClaimAchievementCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IClock clock,
    IDomainEventDispatcher events)
    : ICommandHandler<ClaimAchievementCommand, UnlockResultDto>
{
    public async Task<UnlockResultDto> Handle(ClaimAchievementCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = clock.Now;

        var achievement = await db.Achievements.FirstOrDefaultAsync(a => a.Id == command.AchievementId, ct)
            ?? throw new DomainException("That achievement does not exist.");

        // 1. Hidden ones are earned by living, not by tapping a button.
        if (achievement.IsHidden || achievement.VerificationType == VerificationType.Auto)
            throw new DomainException("Nice try. This one unlocks itself when you earn it.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("Your account could not be found.");

        var existing = await db.UserAchievements
            .Where(ua => ua.UserId == userId && ua.AchievementId == achievement.Id)
            .ToListAsync(ct);

        if (existing.Any(ua => ua.Status == ClaimStatus.Pending))
            throw new DomainException($"\"{achievement.Name}\" is already waiting on a friend to verify it.");

        var approved = existing.Where(ua => ua.Status == ClaimStatus.Approved).ToList();

        // 2. One-shot achievements can only be unlocked once.
        if (!achievement.IsRepeatable && approved.Count > 0)
            throw new DomainException($"You already unlocked \"{achievement.Name}\". Go find a new one.");

        // 3. Repeatables respect their cooldown.
        if (achievement.IsRepeatable && achievement.CooldownHours is { } cooldown && approved.Count > 0)
        {
            var last = approved.Max(ua => ua.ApprovedAt ?? ua.ClaimedAt);
            var readyAt = last.AddHours(cooldown);
            if (now < readyAt)
            {
                var wait = readyAt - now;
                var human = wait.TotalHours >= 1
                    ? $"{(int)wait.TotalHours}h {wait.Minutes}m"
                    : $"{Math.Max(1, (int)wait.TotalMinutes)}m";
                throw new DomainException($"\"{achievement.Name}\" is cooling down. Try again in {human}.");
            }
        }

        // 3b. A multi-step achievement has to actually be finished first.
        AchievementProgress? run = null;
        if (achievement.HasProgress)
        {
            var target = achievement.TargetCount!.Value;
            run = await db.AchievementProgress
                .FirstOrDefaultAsync(p => p.UserId == userId && p.AchievementId == achievement.Id, ct);

            run?.ResetIfWindowLapsed(achievement.ProgressWindow, now);

            var current = run?.Current ?? 0;
            if (current < target)
            {
                var unit = achievement.ProgressUnit ?? "steps";
                throw new DomainException(
                    $"\"{achievement.Name}\" is not finished yet - {current} of {target} {unit}. Keep going.");
            }
        }

        var claim = new UserAchievement
        {
            UserId = userId,
            AchievementId = achievement.Id,
            ClaimedAt = now,
            Status = ClaimStatus.Pending
        };

        // 4. Friend-verified claims stop here - no XP until someone vouches for you (Phase 2).
        if (achievement.VerificationType == VerificationType.Friend)
        {
            db.UserAchievements.Add(claim);
            await db.SaveChangesAsync(ct);

            var pendingProgress = LevelCurve.ProgressFor(user.TotalXp);
            return new UnlockResultDto(
                claim.Id,
                achievement.Id, achievement.Code, achievement.Name, achievement.Description,
                achievement.Tier.ToString(), achievement.Icon, AchievementStatuses.Pending,
                XpGained: 0, NewTotalXp: user.TotalXp,
                OldLevel: pendingProgress.Level, NewLevel: pendingProgress.Level,
                NewLevelTitle: pendingProgress.Title, LeveledUp: false,
                XpToNextLevel: pendingProgress.XpToNextLevel,
                ProgressPercent: pendingProgress.ProgressPercent,
                BonusUnlocks: []);
        }

        // 5. Self-verified: approve, write the ledger and update the cached total - one transaction.
        var xpBefore = user.TotalXp;
        var oldLevel = LevelCurve.LevelFor(xpBefore);

        await using var tx = await db.BeginTransactionAsync(ct);

        claim.Approve(now);
        db.UserAchievements.Add(claim);

        db.XpTransactions.Add(new XpTransaction
        {
            UserId = userId,
            Amount = achievement.XpReward,
            Reason = $"Unlocked {achievement.Name}",
            SourceUserAchievementId = claim.Id,
            CreatedAt = now
        });

        user.TotalXp = xpBefore + achievement.XpReward;

        // The run has been cashed in; start the next one from zero.
        if (run is not null)
        {
            run.Current = 0;
            run.WindowStartedAt = now;
            run.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        // 6. Event handlers evaluate the hidden rules and may add more ledger rows.
        var unlocked = AchievementUnlocked.From(claim, achievement, xpBefore, user.TotalXp, now);
        await events.Dispatch(unlocked, ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        // 7. Build the result from the state after everything (bonuses included) landed.
        var bonuses = await LoadBonusUnlocks(unlocked.BonusUserAchievementIds, ct);
        var progress = LevelCurve.ProgressFor(user.TotalXp);

        return new UnlockResultDto(
            claim.Id,
            achievement.Id, achievement.Code, achievement.Name, achievement.Description,
            achievement.Tier.ToString(), achievement.Icon, AchievementStatuses.Unlocked,
            XpGained: achievement.XpReward,
            NewTotalXp: user.TotalXp,
            OldLevel: oldLevel,
            NewLevel: progress.Level,
            NewLevelTitle: progress.Title,
            LeveledUp: progress.Level > oldLevel,
            XpToNextLevel: progress.XpToNextLevel,
            ProgressPercent: progress.ProgressPercent,
            BonusUnlocks: bonuses);
    }

    /// <summary>Hidden achievements the event handlers auto-unlocked as part of this same claim.</summary>
    private async Task<List<BonusUnlockDto>> LoadBonusUnlocks(List<Guid> bonusClaimIds, CancellationToken ct)
    {
        if (bonusClaimIds.Count == 0) return [];

        return await db.UserAchievements
            .Where(ua => bonusClaimIds.Contains(ua.Id))
            .Select(ua => new BonusUnlockDto(
                ua.Achievement.Id,
                ua.Achievement.Code,
                ua.Achievement.Name,
                ua.Achievement.Description,
                ua.Achievement.Tier.ToString(),
                ua.Achievement.Icon,
                ua.Achievement.XpReward))
            .ToListAsync(ct);
    }
}
