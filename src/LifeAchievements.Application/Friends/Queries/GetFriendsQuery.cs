using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Queries;

public record GetFriendsQuery : IQuery<FriendsOverviewDto>;

public class GetFriendsQueryHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<GetFriendsQuery, FriendsOverviewDto>
{
    public async Task<FriendsOverviewDto> Handle(GetFriendsQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var rows = await db.Friendships.AsNoTracking()
            .Where(f => f.Status != FriendshipStatus.Declined
                        && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => new { f.Id, f.RequesterId, f.AddresseeId, f.Status, f.CreatedAt, f.RespondedAt })
            .ToListAsync(ct);

        if (rows.Count == 0) return new FriendsOverviewDto([], []);

        var otherIds = rows
            .Select(r => r.RequesterId == userId ? r.AddresseeId : r.RequesterId)
            .Distinct()
            .ToList();

        var people = await db.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.TotalXp })
            .ToDictionaryAsync(u => u.Id, ct);

        // One pass over the friends' approved unlocks, so streaks cost a single query.
        var friendIds = rows
            .Where(r => r.Status == FriendshipStatus.Accepted)
            .Select(r => r.RequesterId == userId ? r.AddresseeId : r.RequesterId)
            .ToList();

        var unlockDays = await db.UserAchievements.AsNoTracking()
            .Where(ua => friendIds.Contains(ua.UserId)
                         && ua.Status == ClaimStatus.Approved
                         && ua.ApprovedAt != null)
            .Select(ua => new { ua.UserId, At = ua.ApprovedAt!.Value })
            .ToListAsync(ct);

        var byUser = unlockDays.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.At).ToList());

        var friends = new List<FriendDto>();
        var requests = new List<FriendRequestDto>();

        foreach (var row in rows)
        {
            var otherId = row.RequesterId == userId ? row.AddresseeId : row.RequesterId;
            if (!people.TryGetValue(otherId, out var person)) continue;

            var progress = LevelCurve.ProgressFor(person.TotalXp);

            if (row.Status == FriendshipStatus.Accepted)
            {
                byUser.TryGetValue(otherId, out var days);
                friends.Add(new FriendDto(
                    otherId,
                    person.DisplayName,
                    progress.Level,
                    progress.Title,
                    person.TotalXp,
                    StreakCalculator.Calculate(days ?? [], clock.Now),
                    row.RespondedAt ?? row.CreatedAt));
            }
            else
            {
                requests.Add(new FriendRequestDto(
                    row.Id,
                    otherId,
                    person.DisplayName,
                    progress.Level,
                    progress.Title,
                    row.AddresseeId == userId ? RequestDirections.Incoming : RequestDirections.Outgoing,
                    row.CreatedAt));
            }
        }

        return new FriendsOverviewDto(
            friends.OrderByDescending(f => f.TotalXp).ToList(),
            // Things needing your answer come first.
            requests
                .OrderBy(r => r.Direction == RequestDirections.Incoming ? 0 : 1)
                .ThenByDescending(r => r.SentAt)
                .ToList());
    }
}
