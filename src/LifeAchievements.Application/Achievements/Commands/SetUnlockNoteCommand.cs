using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Achievements.Commands;

public record SetUnlockNoteCommand(Guid UserAchievementId, string? Note) : ICommand<bool>;

public class SetUnlockNoteCommandValidator : AbstractValidator<SetUnlockNoteCommand>
{
    public SetUnlockNoteCommandValidator()
    {
        RuleFor(x => x.UserAchievementId).NotEmpty().WithMessage("Which unlock? No id was given.");
        RuleFor(x => x.Note).MaximumLength(UserAchievement.MaxNoteLength)
            .WithMessage($"Keep it under {UserAchievement.MaxNoteLength} characters.");
    }
}

/// <summary>
/// Attaches (or clears) the one-line memory on an unlock. Separate from the claim so the
/// trophy screen appears instantly and the note can be written while the moment is fresh.
/// </summary>
public class SetUnlockNoteCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<SetUnlockNoteCommand, bool>
{
    public async Task<bool> Handle(SetUnlockNoteCommand command, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var claim = await db.UserAchievements
            .FirstOrDefaultAsync(ua => ua.Id == command.UserAchievementId && ua.UserId == userId, ct)
            ?? throw new DomainException("That unlock could not be found.");

        var note = command.Note?.Trim();
        claim.Note = string.IsNullOrWhiteSpace(note) ? null : note;

        await db.SaveChangesAsync(ct);
        return true;
    }
}
