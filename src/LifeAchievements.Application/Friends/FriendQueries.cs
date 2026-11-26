using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends;

/// <summary>
/// Who counts as a friend, in one place. Friendship is stored as a single row per pair in
/// whichever direction the request went, so every read has to check both columns.
/// </summary>
public static class FriendQueries
{
    public static async Task<List<Guid>> AcceptedFriendIds(IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var rows = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted
                        && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => new { f.RequesterId, f.AddresseeId })
            .ToListAsync(ct);

        return rows
            .Select(r => r.RequesterId == userId ? r.AddresseeId : r.RequesterId)
            .Distinct()
            .ToList();
    }

    public static async Task<bool> AreFriends(IAppDbContext db, Guid a, Guid b, CancellationToken ct) =>
        a != b && await db.Friendships.AsNoTracking().AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted
            && ((f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a)), ct);
}
