using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Achievements.Queries;

public record GetRecentUnlocksQuery(int Take = 10) : IQuery<IReadOnlyList<RecentUnlockDto>>;

public class GetRecentUnlocksQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetRecentUnlocksQuery, IReadOnlyList<RecentUnlockDto>>
{
    public async Task<IReadOnlyList<RecentUnlockDto>> Handle(GetRecentUnlocksQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var take = Math.Clamp(query.Take, 1, 50);

        return await db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId && ua.Status == ClaimStatus.Approved)
            .OrderByDescending(ua => ua.ApprovedAt)
            .Take(take)
            .Select(ua => new RecentUnlockDto(
                ua.Id,
                ua.AchievementId,
                ua.Achievement.Name,
                ua.Achievement.Icon,
                ua.Achievement.Tier.ToString(),
                ua.Achievement.XpReward,
                ua.ApprovedAt!.Value,
                ua.Note))
            .ToListAsync(ct);
    }
}
