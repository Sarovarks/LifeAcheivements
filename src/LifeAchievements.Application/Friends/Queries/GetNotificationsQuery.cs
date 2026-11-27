using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Queries;

public record GetNotificationsQuery(int Take = 30) : IQuery<NotificationsDto>;

/// <summary>
/// The three things that already happened without telling you: someone reacted to your
/// unlock, someone asked to be your friend, or a friend's claim is waiting on your word.
///
/// Nothing is stored. These are read from the rows that already exist, so there is no second
/// copy of the truth to drift, and no table to clean up. "New" is simply anything that
/// happened after the last time you opened the bell.
/// </summary>
public class GetNotificationsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetNotificationsQuery, NotificationsDto>
{
    public async Task<NotificationsDto> Handle(GetNotificationsQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var take = Math.Clamp(query.Take, 1, 100);

        var me = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.LastSeenNotificationsAt })
            .FirstOrDefaultAsync(ct);

        var lastSeen = me?.LastSeenNotificationsAt;

        // Ordering and trimming happen in memory, below. SQLite - which the tests run on -
        // cannot ORDER BY a DateTimeOffset, and these three sets are bounded by one person's
        // own activity, so reading them whole costs little. A global feed would need a UTC
        // DateTime column and a database-side sort instead.
        var items = new List<NotificationDto>();

        // --- someone reacted to one of my unlocks -------------------------------------
        //
        // r.UserId != userId matters beyond the rule that now forbids it: reactions people
        // left on their own unlocks before that rule existed are still in the database, and
        // nobody needs telling that they cheered themselves.
        var reactions = await db.Reactions.AsNoTracking()
            .Where(r => r.UserAchievement.UserId == userId && r.UserId != userId)
            .Select(r => new
            {
                r.UserAchievementId,
                r.UserId,
                r.Type,
                r.CreatedAt,
                Achievement = r.UserAchievement.Achievement.Name
            })
            .ToListAsync(ct);

        // --- someone wants to be my friend --------------------------------------------
        var requests = await db.Friendships.AsNoTracking()
            .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
            .Select(f => new { f.Id, f.RequesterId, f.CreatedAt })
            .ToListAsync(ct);

        // --- a friend is waiting for me to vouch for them -----------------------------
        var friendIds = await FriendQueries.AcceptedFriendIds(db, userId, ct);

        var waiting = await db.UserAchievements.AsNoTracking()
            .Where(ua => friendIds.Contains(ua.UserId)
                         && ua.Status == ClaimStatus.Pending
                         && ua.Achievement.VerificationType == VerificationType.Friend)
            .Select(ua => new { ua.Id, ua.UserId, ua.ClaimedAt, Achievement = ua.Achievement.Name })
            .ToListAsync(ct);

        // One lookup for every name mentioned above.
        var people = reactions.Select(r => r.UserId)
            .Concat(requests.Select(r => r.RequesterId))
            .Concat(waiting.Select(w => w.UserId))
            .Distinct()
            .ToList();

        var names = await db.Users.AsNoTracking()
            .Where(u => people.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        string Name(Guid id) =>
            names.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : "Someone";

        bool IsNew(DateTimeOffset at) => lastSeen is null || at > lastSeen;

        items.AddRange(reactions.Select(r => new NotificationDto(
            "Reaction",
            r.Type == ReactionType.Cheer
                ? $"{Name(r.UserId)} cheered you"
                : $"{Name(r.UserId)} respects that",
            r.Achievement,
            r.CreatedAt,
            IsNew(r.CreatedAt),
            r.UserAchievementId,
            null)));

        items.AddRange(requests.Select(r => new NotificationDto(
            "FriendRequest",
            $"{Name(r.RequesterId)} wants to be friends",
            "Tap Friends to answer",
            r.CreatedAt,
            IsNew(r.CreatedAt),
            null,
            r.Id)));

        items.AddRange(waiting.Select(w => new NotificationDto(
            "Verification",
            $"{Name(w.UserId)} needs you to vouch",
            w.Achievement,
            w.ClaimedAt,
            IsNew(w.ClaimedAt),
            w.Id,
            null)));

        var ordered = items.OrderByDescending(i => i.At).Take(take).ToList();

        return new NotificationsDto(ordered.Count(i => i.IsNew), ordered);
    }
}
