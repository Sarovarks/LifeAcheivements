using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Queries;

public record GetFeedQuery(int Take = 30) : IQuery<IReadOnlyList<FeedItemDto>>;

/// <summary>
/// What your friends have been up to, newest first - yours included, so the feed is not empty
/// on day one. Notes are carried through, which is what makes it worth reading rather than a
/// list of badge names.
/// </summary>
public class GetFeedQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetFeedQuery, IReadOnlyList<FeedItemDto>>
{
    public async Task<IReadOnlyList<FeedItemDto>> Handle(GetFeedQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var take = Math.Clamp(query.Take, 1, 100);

        var friendIds = await FriendQueries.AcceptedFriendIds(db, userId, ct);
        var visible = friendIds.Append(userId).ToList();

        var unlocks = await db.UserAchievements.AsNoTracking()
            .Where(ua => visible.Contains(ua.UserId)
                         && ua.Status == ClaimStatus.Approved
                         && !ua.Achievement.IsHidden)
            .OrderByDescending(ua => ua.ApprovedAt)
            .Take(take)
            .Select(ua => new
            {
                ua.Id,
                ua.UserId,
                ua.AchievementId,
                ua.Achievement.Name,
                ua.Achievement.Description,
                ua.Achievement.Icon,
                ua.Achievement.Tier,
                ua.Achievement.XpReward,
                ua.Note,
                At = ua.ApprovedAt!.Value
            })
            .ToListAsync(ct);

        if (unlocks.Count == 0) return [];

        var people = await db.Users.AsNoTracking()
            .Where(u => visible.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.TotalXp })
            .ToDictionaryAsync(u => u.Id, ct);

        var ids = unlocks.Select(u => u.Id).ToList();
        var reactions = await db.Reactions.AsNoTracking()
            .Where(r => ids.Contains(r.UserAchievementId))
            .Select(r => new { r.UserAchievementId, r.UserId, r.Type })
            .ToListAsync(ct);

        var byUnlock = reactions.GroupBy(r => r.UserAchievementId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return unlocks.Select(u =>
        {
            people.TryGetValue(u.UserId, out var person);
            byUnlock.TryGetValue(u.Id, out var mine);
            mine ??= [];

            return new FeedItemDto(
                u.Id,
                u.UserId,
                person?.DisplayName ?? "Someone",
                LevelCurve.LevelFor(person?.TotalXp ?? 0),
                u.AchievementId,
                u.Name,
                u.Description,
                u.Icon,
                u.Tier.ToString(),
                u.XpReward,
                u.Note,
                u.At,
                Cheers: mine.Count(r => r.Type == ReactionType.Cheer),
                Respects: mine.Count(r => r.Type == ReactionType.Respect),
                ICheered: mine.Any(r => r.UserId == userId && r.Type == ReactionType.Cheer),
                IRespected: mine.Any(r => r.UserId == userId && r.Type == ReactionType.Respect),
                IsMine: u.UserId == userId);
        }).ToList();
    }
}
