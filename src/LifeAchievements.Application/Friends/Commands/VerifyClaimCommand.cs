using FluentValidation;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Commands;

public record VerifyClaimCommand(Guid UserAchievementId, bool Approve) : ICommand<VerifyClaimResultDto>;

public class VerifyClaimCommandValidator : AbstractValidator<VerifyClaimCommand>
{
    public VerifyClaimCommandValidator()
    {
        RuleFor(x => x.UserAchievementId).NotEmpty().WithMessage("Which claim? No id was given.");
    }
}

/// <summary>
/// A friend vouching for a pending claim - the other half of VerificationType.Friend, which
/// has been parking claims since Phase 1. Approval goes through the same UnlockService as a
/// self-claim, so the ledger, hidden achievements and level-up all behave identically.
/// </summary>
public class VerifyClaimCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IClock clock,
    IUnlockService unlocks)
    : ICommandHandler<VerifyClaimCommand, VerifyClaimResultDto>
{
    public async Task<VerifyClaimResultDto> Handle(VerifyClaimCommand command, CancellationToken ct)
    {
        var verifierId = currentUser.Id;
        var now = clock.Now;

        var claim = await db.UserAchievements
            .Include(ua => ua.Achievement)
            .FirstOrDefaultAsync(ua => ua.Id == command.UserAchievementId, ct)
            ?? throw new DomainException("That claim could not be found.");

        if (claim.UserId == verifierId)
            throw new DomainException("You cannot verify your own claim. That is rather the point.");

        if (claim.Status != ClaimStatus.Pending)
            throw new DomainException("That claim has already been settled.");

        if (claim.Achievement.VerificationType != VerificationType.Friend)
            throw new DomainException("That achievement does not need verifying.");

        if (!await FriendQueries.AreFriends(db, verifierId, claim.UserId, ct))
            throw new DomainException("You can only verify a friend's claim.");

        var claimant = await db.Users.FirstOrDefaultAsync(u => u.Id == claim.UserId, ct)
            ?? throw new DomainException("That player could not be found.");

        if (!command.Approve)
        {
            claim.Status = ClaimStatus.Rejected;
            claim.VerifiedByUserId = verifierId;
            await db.SaveChangesAsync(ct);

            return new VerifyClaimResultDto(
                claim.Id, claimant.DisplayName, claim.Achievement.Name, Approved: false, XpAwarded: 0);
        }

        await unlocks.ApproveAsync(
            claim, claim.Achievement, claimant, run: null, now, verifiedBy: verifierId, isNewClaim: false, ct);

        return new VerifyClaimResultDto(
            claim.Id,
            claimant.DisplayName,
            claim.Achievement.Name,
            Approved: true,
            // The achievement's own reward. Any hidden achievement it set off is the
            // claimant's surprise to discover, not something to report to the verifier.
            XpAwarded: claim.Achievement.XpReward);
    }
}
