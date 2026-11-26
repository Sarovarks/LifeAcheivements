using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Commands;

public record SendFriendRequestCommand(string Email) : ICommand<Guid>;

public class SendFriendRequestCommandValidator : AbstractValidator<SendFriendRequestCommand>
{
    public SendFriendRequestCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Whose email?")
            .EmailAddress().WithMessage("That does not look like an email address.");
    }
}

/// <summary>
/// Friends are added by exact email. Deliberately not a name search: that would let anybody
/// enumerate the users of the app, and nobody needs that to add the three people they know.
/// </summary>
public class SendFriendRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<SendFriendRequestCommand, Guid>
{
    public async Task<Guid> Handle(SendFriendRequestCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var email = command.Email.Trim();

        var target = await db.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), ct)
            ?? throw new DomainException("Nobody is playing with that email yet. Invite them?");

        if (target.Id == userId)
            throw new DomainException("You cannot add yourself. Impressive attempt, though.");

        var existing = await db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == target.Id)
            || (f.RequesterId == target.Id && f.AddresseeId == userId), ct);

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case FriendshipStatus.Accepted:
                    throw new DomainException($"You and {target.DisplayName} are already friends.");

                case FriendshipStatus.Pending when existing.RequesterId == userId:
                    throw new DomainException($"You have already asked {target.DisplayName}. Give them a moment.");

                case FriendshipStatus.Pending:
                    // They asked first - treat this as accepting rather than crossing requests.
                    existing.Accept(clock.Now);
                    await db.SaveChangesAsync(ct);
                    return existing.Id;

                case FriendshipStatus.Declined:
                    // A previous no does not have to be forever; reopen the same row.
                    existing.RequesterId = userId;
                    existing.AddresseeId = target.Id;
                    existing.Status = FriendshipStatus.Pending;
                    existing.CreatedAt = clock.Now;
                    existing.RespondedAt = null;
                    await db.SaveChangesAsync(ct);
                    return existing.Id;
            }
        }

        var friendship = new Friendship
        {
            RequesterId = userId,
            AddresseeId = target.Id,
            Status = FriendshipStatus.Pending,
            CreatedAt = clock.Now
        };

        db.Friendships.Add(friendship);
        await db.SaveChangesAsync(ct);

        return friendship.Id;
    }
}
