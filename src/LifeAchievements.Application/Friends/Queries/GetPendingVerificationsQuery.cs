using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Queries;

public record GetPendingVerificationsQuery : IQuery<IReadOnlyList<PendingVerificationDto>>;

/// <summary>Friends' claims that are waiting for somebody to vouch for them.</summary>
public class GetPendingVerificationsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetPendingVerificationsQuery, IReadOnlyList<PendingVerificationDto>>
{
    public async Task<IReadOnlyList<PendingVerificationDto>> Handle(
        GetPendingVerificationsQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var friendIds = await FriendQueries.AcceptedFriendIds(db, userId, ct);
        if (friendIds.Count == 0) return [];

        var claims = await db.UserAchievements.AsNoTracking()
            .Where(ua => friendIds.Contains(ua.UserId)
                         && ua.Status == ClaimStatus.Pending
                         && ua.Achievement.VerificationType == VerificationType.Friend)
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
                ua.ClaimedAt
            })
            .ToListAsync(ct);

        if (claims.Count == 0) return [];

        var people = await db.Users.AsNoTracking()
            .Where(u => friendIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.TotalXp })
            .ToDictionaryAsync(u => u.Id, ct);

        return claims
            .OrderByDescending(c => c.ClaimedAt)
            .Select(c =>
            {
                people.TryGetValue(c.UserId, out var person);
                return new PendingVerificationDto(
                    c.Id,
                    c.UserId,
                    person?.DisplayName ?? "Someone",
                    LevelCurve.LevelFor(person?.TotalXp ?? 0),
                    c.AchievementId,
                    c.Name,
                    c.Description,
                    c.Icon,
                    c.Tier.ToString(),
                    c.XpReward,
                    c.Note,
                    c.ClaimedAt);
            })
            .ToList();
    }
}
