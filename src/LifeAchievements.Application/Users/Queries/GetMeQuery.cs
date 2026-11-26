using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Users.Queries;

public record GetMeQuery : IQuery<MeDto>;

public class GetMeQueryHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<GetMeQuery, MeDto>
{
    public async Task<MeDto> Handle(GetMeQuery query, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainException("Your account could not be found.");

        var approvedAt = await db.UserAchievements
            .Where(ua => ua.UserId == userId && ua.Status == ClaimStatus.Approved && ua.ApprovedAt != null)
            .Select(ua => ua.ApprovedAt!.Value)
            .ToListAsync(ct);

        var progress = LevelCurve.ProgressFor(user.TotalXp);
        var streak = StreakCalculator.Calculate(approvedAt, clock.Now);

        // Friends are Phase 2 - until then everybody is gloriously rank 1 of 1.
        const int friendsRank = 1;

        return new MeDto(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.TotalXp,
            progress.Level,
            progress.Title,
            progress.XpToNextLevel,
            progress.NextLevelAtXp,
            Math.Round(progress.ProgressPercent, 1),
            approvedAt.Count,
            streak,
            friendsRank);
    }
}
