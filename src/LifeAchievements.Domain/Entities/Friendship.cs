using LifeAchievements.Domain.Enums;

namespace LifeAchievements.Domain.Entities;

/// <summary>
/// One row per pair of people, in whichever direction the request was sent. There is never a
/// second row for the reverse direction - queries check both columns instead.
/// </summary>
public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Who sent the request.</summary>
    public Guid RequesterId { get; set; }

    /// <summary>Who was asked, and who is the only one able to accept it.</summary>
    public Guid AddresseeId { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public void Accept(DateTimeOffset at)
    {
        Status = FriendshipStatus.Accepted;
        RespondedAt = at;
    }

    public void Decline(DateTimeOffset at)
    {
        Status = FriendshipStatus.Declined;
        RespondedAt = at;
    }

    public bool Involves(Guid userId) => RequesterId == userId || AddresseeId == userId;

    /// <summary>The other person, from the point of view of the user asking.</summary>
    public Guid OtherParty(Guid userId) => RequesterId == userId ? AddresseeId : RequesterId;

    /// <summary>
    /// A stable ordering of the pair, so a uniqueness check cannot be dodged by sending the
    /// request the other way round.
    /// </summary>
    public static (Guid Low, Guid High) PairKey(Guid a, Guid b) =>
        a.CompareTo(b) <= 0 ? (a, b) : (b, a);
}
