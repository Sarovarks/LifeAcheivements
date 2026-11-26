using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Achievements.Queries;

public record GetAchievementsQuery(string? Category) : IQuery<IReadOnlyList<AchievementDto>>;

public class GetAchievementsQueryHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<GetAchievementsQuery, IReadOnlyList<AchievementDto>>
{
    public async Task<IReadOnlyList<AchievementDto>> Handle(GetAchievementsQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = clock.Now;

        var achievements = db.Achievements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Category)
            && Enum.TryParse<AchievementCategory>(query.Category, ignoreCase: true, out var category))
        {
            achievements = achievements.Where(a => a.Category == category);
        }

        var list = await achievements
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Tier)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);

        var mine = await db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .Select(ua => new { ua.AchievementId, ua.Status, ua.ApprovedAt, ua.ClaimedAt })
            .ToListAsync(ct);

        var byAchievement = mine.GroupBy(x => x.AchievementId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var progressRows = await db.AchievementProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);
        var progressByAchievement = progressRows.ToDictionary(p => p.AchievementId);

        return list.Select(a =>
        {
            byAchievement.TryGetValue(a.Id, out var claims);
            claims ??= [];

            var approved = claims.Where(c => c.Status == ClaimStatus.Approved).ToList();
            var hasPending = claims.Any(c => c.Status == ClaimStatus.Pending);
            var timesUnlocked = approved.Count;
            var lastUnlockedAt = approved.Count == 0 ? (DateTimeOffset?)null : approved.Max(c => c.ApprovedAt ?? c.ClaimedAt);

            DateTimeOffset? claimableAgainAt = null;
            if (a.IsRepeatable && a.CooldownHours is { } hours && lastUnlockedAt is { } last)
                claimableAgainAt = last.AddHours(hours);

            ProgressDto? progress = null;
            if (a.HasProgress)
            {
                progressByAchievement.TryGetValue(a.Id, out var row);

                // A lapsed run reads as zero without needing a write on a GET.
                if (row is not null && ProgressRules.HasWindowLapsed(a.ProgressWindow, row.WindowStartedAt, now))
                    row = null;

                progress = ProgressMapper.ToDto(a, row);
            }

            var status = ResolveStatus(a, timesUnlocked, hasPending, claimableAgainAt, now, progress);
            var unlockedEver = timesUnlocked > 0;

            // Hidden achievements stay masked until they are actually earned.
            if (a.IsHidden && !unlockedEver)
            {
                return new AchievementDto(
                    a.Id, a.Code, "Hidden achievement",
                    a.Hint ?? "Keep playing. You will find it.",
                    a.Category.ToString(), a.Tier.ToString(),
                    XpReward: 0, IsHidden: true, Hint: a.Hint,
                    IsRepeatable: a.IsRepeatable, CooldownHours: a.CooldownHours,
                    VerificationType: a.VerificationType.ToString(), Icon: "lock",
                    Status: AchievementStatuses.Locked, TimesUnlocked: 0,
                    LastUnlockedAt: null, ClaimableAgainAt: null, Progress: null);
            }

            return new AchievementDto(
                a.Id, a.Code, a.Name, a.Description,
                a.Category.ToString(), a.Tier.ToString(), a.XpReward,
                a.IsHidden, a.Hint, a.IsRepeatable, a.CooldownHours,
                a.VerificationType.ToString(), a.Icon,
                status, timesUnlocked, lastUnlockedAt,
                status == AchievementStatuses.Cooldown ? claimableAgainAt : null,
                progress);
        }).ToList();
    }

    private static string ResolveStatus(
        Achievement a, int timesUnlocked, bool hasPending, DateTimeOffset? claimableAgainAt,
        DateTimeOffset now, ProgressDto? progress)
    {
        if (hasPending) return AchievementStatuses.Pending;

        // Hidden / Auto ones can never be claimed by hand.
        if (a.IsHidden || a.VerificationType == VerificationType.Auto)
            return timesUnlocked > 0 ? AchievementStatuses.Unlocked : AchievementStatuses.Locked;

        // A multi-step achievement is only claimable once the run is finished.
        if (progress is { IsComplete: false } && timesUnlocked == 0)
            return AchievementStatuses.InProgress;

        if (timesUnlocked == 0) return AchievementStatuses.Claimable;
        if (!a.IsRepeatable) return AchievementStatuses.Unlocked;
        if (claimableAgainAt is { } readyAt && now < readyAt) return AchievementStatuses.Cooldown;

        return AchievementStatuses.Claimable;
    }
}
