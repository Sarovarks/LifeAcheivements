using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Commands;

public record ToggleReactionCommand(Guid UserAchievementId, string Type) : ICommand<ReactionResultDto>;

public class ToggleReactionCommandValidator : AbstractValidator<ToggleReactionCommand>
{
    public ToggleReactionCommandValidator()
    {
        RuleFor(x => x.UserAchievementId).NotEmpty().WithMessage("Which unlock? No id was given.");
        RuleFor(x => x.Type).Must(t => Enum.TryParse<ReactionType>(t, ignoreCase: true, out _))
            .WithMessage("A reaction is either a Cheer or a Respect.");
    }
}

/// <summary>
/// Tapping the same reaction again takes it back, so the buttons are a toggle rather than a
/// one-way commitment. Cheer and Respect are independent - you can give both.
/// </summary>
public class ToggleReactionCommandHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ToggleReactionCommand, ReactionResultDto>
{
    public async Task<ReactionResultDto> Handle(ToggleReactionCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var type = Enum.Parse<ReactionType>(command.Type, ignoreCase: true);

        var claim = await db.UserAchievements.AsNoTracking()
            .FirstOrDefaultAsync(ua => ua.Id == command.UserAchievementId, ct)
            ?? throw new DomainException("That unlock could not be found.");

        if (claim.Status != ClaimStatus.Approved)
            throw new DomainException("You can only react to something that actually happened.");

        // You can only react to your own or a friend's unlock - not a stranger's.
        if (claim.UserId != userId && !await FriendQueries.AreFriends(db, userId, claim.UserId, ct))
            throw new DomainException("You can only react to a friend's unlock.");

        var existing = await db.Reactions.FirstOrDefaultAsync(r =>
            r.UserAchievementId == command.UserAchievementId && r.UserId == userId && r.Type == type, ct);

        if (existing is not null)
        {
            db.Reactions.Remove(existing);
        }
        else
        {
            db.Reactions.Add(new Reaction
            {
                UserAchievementId = command.UserAchievementId,
                UserId = userId,
                Type = type,
                CreatedAt = clock.Now
            });
        }

        await db.SaveChangesAsync(ct);

        var all = await db.Reactions.AsNoTracking()
            .Where(r => r.UserAchievementId == command.UserAchievementId)
            .Select(r => new { r.UserId, r.Type })
            .ToListAsync(ct);

        return new ReactionResultDto(
            command.UserAchievementId,
            all.Count(r => r.Type == ReactionType.Cheer),
            all.Count(r => r.Type == ReactionType.Respect),
            all.Any(r => r.UserId == userId && r.Type == ReactionType.Cheer),
            all.Any(r => r.UserId == userId && r.Type == ReactionType.Respect));
    }
}
