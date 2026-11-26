using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Achievements.Commands;

public record LogProgressCommand(Guid AchievementId, int Amount = 1) : ICommand<LogProgressResultDto>;

public class LogProgressCommandValidator : AbstractValidator<LogProgressCommand>
{
    public LogProgressCommandValidator()
    {
        RuleFor(x => x.AchievementId).NotEmpty().WithMessage("Which achievement? No id was given.");
        RuleFor(x => x.Amount).InclusiveBetween(1, 100)
            .WithMessage("Log between 1 and 100 steps at a time.");
    }
}

/// <summary>
/// Adds a step towards a multi-step achievement. Reaching the target does not unlock anything
/// by itself - it makes the achievement claimable, so the player still gets the trophy moment.
/// </summary>
public class LogProgressCommandHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<LogProgressCommand, LogProgressResultDto>
{
    public async Task<LogProgressResultDto> Handle(LogProgressCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = clock.Now;

        var achievement = await db.Achievements.FirstOrDefaultAsync(a => a.Id == command.AchievementId, ct)
            ?? throw new DomainException("That achievement does not exist.");

        if (!achievement.HasProgress)
            throw new DomainException($"\"{achievement.Name}\" is a single thing you either did or did not do. Just claim it.");

        if (achievement.IsHidden)
            throw new DomainException("Nice try. This one unlocks itself when you earn it.");

        var target = achievement.TargetCount!.Value;

        var progress = await db.AchievementProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.AchievementId == achievement.Id, ct);

        if (progress is null)
        {
            progress = new AchievementProgress
            {
                UserId = userId,
                AchievementId = achievement.Id,
                Current = 0,
                WindowStartedAt = now,
                UpdatedAt = now
            };
            db.AchievementProgress.Add(progress);
        }
        else
        {
            // A run from a lapsed week should not quietly count towards this one.
            progress.ResetIfWindowLapsed(achievement.ProgressWindow, now);
        }

        var wasComplete = progress.IsComplete(target);
        if (wasComplete)
            throw new DomainException($"\"{achievement.Name}\" is already finished - claim it and take the XP.");

        progress.Log(command.Amount, target, now);
        await db.SaveChangesAsync(ct);

        return new LogProgressResultDto(
            achievement.Id,
            achievement.Name,
            ProgressMapper.ToDto(achievement, progress),
            JustCompleted: progress.IsComplete(target));
    }
}

/// <summary>Shared by the command and the list query so the two can never disagree.</summary>
public static class ProgressMapper
{
    public static ProgressDto ToDto(Achievement achievement, AchievementProgress? progress)
    {
        var target = achievement.TargetCount ?? 0;
        var current = Math.Min(progress?.Current ?? 0, target);

        return new ProgressDto(
            Current: current,
            Target: target,
            Unit: achievement.ProgressUnit ?? "steps",
            Window: achievement.ProgressWindow.ToString(),
            WindowLabel: ProgressRules.Describe(achievement.ProgressWindow),
            Percent: target <= 0 ? 0 : Math.Clamp((double)current / target * 100d, 0d, 100d),
            ResetsAt: progress is null
                ? null
                : ProgressRules.WindowEndsAt(achievement.ProgressWindow, progress.WindowStartedAt));
    }
}
