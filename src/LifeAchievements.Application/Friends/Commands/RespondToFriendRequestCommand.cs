using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Commands;

public record RespondToFriendRequestCommand(Guid FriendshipId, bool Accept) : ICommand<bool>;

public class RespondToFriendRequestCommandValidator : AbstractValidator<RespondToFriendRequestCommand>
{
    public RespondToFriendRequestCommandValidator()
    {
        RuleFor(x => x.FriendshipId).NotEmpty().WithMessage("Which request? No id was given.");
    }
}

public class RespondToFriendRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<RespondToFriendRequestCommand, bool>
{
    public async Task<bool> Handle(RespondToFriendRequestCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var friendship = await db.Friendships.FirstOrDefaultAsync(f => f.Id == command.FriendshipId, ct)
            ?? throw new DomainException("That friend request could not be found.");

        // Only the person who was asked can answer - the sender cannot accept on their behalf.
        if (friendship.AddresseeId != userId)
            throw new DomainException("That request is not yours to answer.");

        if (friendship.Status != FriendshipStatus.Pending)
            throw new DomainException("That request has already been dealt with.");

        if (command.Accept) friendship.Accept(clock.Now);
        else friendship.Decline(clock.Now);

        await db.SaveChangesAsync(ct);
        return command.Accept;
    }
}

// ---------------------------------------------------------------------------

public record RemoveFriendCommand(Guid FriendUserId) : ICommand<bool>;

public class RemoveFriendCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<RemoveFriendCommand, bool>
{
    public async Task<bool> Handle(RemoveFriendCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var friendship = await db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == command.FriendUserId)
            || (f.RequesterId == command.FriendUserId && f.AddresseeId == userId), ct)
            ?? throw new DomainException("You are not friends with them.");

        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
