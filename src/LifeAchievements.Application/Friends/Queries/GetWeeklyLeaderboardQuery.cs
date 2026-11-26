using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Queries;

public record GetWeeklyLeaderboardQuery : IQuery<WeeklyLeaderboardDto>;

/// <summary>
/// XP earned since Monday, you and your friends. Summed straight from the ledger rather than
/// from Users.TotalXp, because the cached total is lifetime XP and says nothing about a week.
/// </summary>
public class GetWeeklyLeaderboardQueryHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<GetWeeklyLeaderboardQuery, WeeklyLeaderboardDto>
{
    public async Task<WeeklyLeaderboardDto> Handle(GetWeeklyLeaderboardQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = clock.Now;

        var localNow = now.ToLocalTime();
        var weekStartLocal = ProgressRules.StartOfWeek(localNow.Date);
        var weekStart = new DateTimeOffset(weekStartLocal, TimeZoneInfo.Local.GetUtcOffset(weekStartLocal));
        var weekEnd = weekStart.AddDays(7);

        var friendIds = await FriendQueries.AcceptedFriendIds(db, userId, ct);
        var everyone = friendIds.Append(userId).Distinct().ToList();

        var people = await db.Users.AsNoTracking()
            .Where(u => everyone.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.TotalXp })
            .ToListAsync(ct);

        // The date window is applied in memory on purpose: SQLite cannot translate a
        // DateTimeOffset comparison, and the tests run against it. The scope here is the
        // player plus their friends, so the row count stays small; a leaderboard across all
        // users would want a UTC DateTime column and a database-side filter instead.
        var xpRows = await db.XpTransactions.AsNoTracking()
            .Where(x => everyone.Contains(x.UserId))
            .Select(x => new { x.UserId, x.Amount, x.CreatedAt })
            .ToListAsync(ct);

        var xpByUser = xpRows
            .Where(x => x.CreatedAt >= weekStart && x.CreatedAt < weekEnd)
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var unlockRows = await db.UserAchievements.AsNoTracking()
            .Where(ua => everyone.Contains(ua.UserId) && ua.Status == ClaimStatus.Approved)
            .Select(ua => new { ua.UserId, ua.ApprovedAt })
            .ToListAsync(ct);

        var unlocksByUser = unlockRows
            .Where(u => u.ApprovedAt >= weekStart && u.ApprovedAt < weekEnd)
            .GroupBy(u => u.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        var entries = people
            .Select(p =>
            {
                var progress = LevelCurve.ProgressFor(p.TotalXp);
                return new
                {
                    p.Id,
                    p.DisplayName,
                    progress.Level,
                    progress.Title,
                    Xp = xpByUser.GetValueOrDefault(p.Id, 0),
                    Unlocks = unlocksByUser.GetValueOrDefault(p.Id, 0)
                };
            })
            // Ties break on lifetime level, then name, so the order never jitters between calls.
            .OrderByDescending(e => e.Xp)
            .ThenByDescending(e => e.Level)
            .ThenBy(e => e.DisplayName)
            .Select((e, index) => new LeaderboardEntryDto(
                Rank: index + 1,
                e.Id,
                e.DisplayName,
                e.Level,
                e.Title,
                e.Xp,
                e.Unlocks,
                IsMe: e.Id == userId))
            .ToList();

        return new WeeklyLeaderboardDto(weekStart, weekEnd, entries);
    }
}
