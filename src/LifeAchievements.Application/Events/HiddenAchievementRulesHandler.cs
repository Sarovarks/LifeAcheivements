using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Events;
using LifeAchievements.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Events;

/// <summary>
/// Runs after every approved unlock and awards any hidden achievement whose rule now holds.
/// Bonus XP goes through the same ledger, so TotalXp is always the sum of XpTransactions.
/// </summary>
public class HiddenAchievementRulesHandler(IAppDbContext db) : IDomainEventHandler<AchievementUnlocked>
{
    public async Task Handle(AchievementUnlocked e, CancellationToken ct)
    {
        var userId = e.UserId;
        var now = e.OccurredAt;

        // Everything already approved for this user, including the claim that triggered us.
        var approved = await db.UserAchievements
            .Where(ua => ua.UserId == userId && ua.Status == ClaimStatus.Approved)
            .Select(ua => new
            {
                ua.AchievementId,
                ua.ApprovedAt,
                ua.Achievement.Category,
                ua.Achievement.IsHidden,
                ua.Achievement.Code
            })
            .ToListAsync(ct);

        var alreadyHas = approved.Select(a => a.Code).ToHashSet();
        var visible = approved.Where(a => !a.IsHidden).ToList();
        var toAward = new List<string>();

        // First Steps - the very first unlock ever.
        if (visible.Count == 1)
            toAward.Add(HiddenAchievements.FirstSteps);

        // Night Owl - claimed something in the small hours.
        if (HiddenAchievements.IsNightOwlHour(now))
            toAward.Add(HiddenAchievements.NightOwl);

        // Deja Vu - the 30th unlock of this same repeatable achievement.
        var sameAchievementCount = visible.Count(a => a.AchievementId == e.AchievementId);
        if (sameAchievementCount >= HiddenAchievements.DejaVuRepeatCount)
            toAward.Add(HiddenAchievements.DejaVu);

        // Overachiever - five unlocks inside one calendar day, in the player's own time.
        var today = now.ToLocalTime().Date;
        var todayCount = visible.Count(a => a.ApprovedAt?.ToLocalTime().Date == today);
        if (todayCount >= HiddenAchievements.OverachieverUnlocksPerDay)
            toAward.Add(HiddenAchievements.Overachiever);

        // Well Rounded - at least one unlock in every single category.
        var categories = visible.Select(a => a.Category).Distinct().Count();
        if (categories >= HiddenAchievements.WellRoundedCategoryCount)
            toAward.Add(HiddenAchievements.WellRounded);

        // Comeback Kid - back after a long quiet stretch.
        var previous = visible
            .Where(a => a.AchievementId != e.AchievementId || a.ApprovedAt != now)
            .Select(a => a.ApprovedAt)
            .Where(d => d is not null && d < now)
            .ToList();
        if (previous.Count > 0)
        {
            var gap = now - previous.Max()!.Value;
            if (gap.TotalDays >= HiddenAchievements.ComebackKidQuietDays)
                toAward.Add(HiddenAchievements.ComebackKid);
        }

        var wanted = toAward.Distinct().Where(code => !alreadyHas.Contains(code)).ToList();
        if (wanted.Count == 0) return;

        var hidden = await db.Achievements
            .Where(a => wanted.Contains(a.Code))
            .ToListAsync(ct);

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

        foreach (var achievement in hidden)
        {
            var claim = new UserAchievement
            {
                UserId = userId,
                AchievementId = achievement.Id,
                ClaimedAt = now,
                Status = ClaimStatus.Approved,
                ApprovedAt = now
            };
            db.UserAchievements.Add(claim);
            e.BonusUserAchievementIds.Add(claim.Id);

            db.XpTransactions.Add(new XpTransaction
            {
                UserId = userId,
                Amount = achievement.XpReward,
                Reason = $"Hidden achievement: {achievement.Name}",
                SourceUserAchievementId = claim.Id,
                CreatedAt = now
            });

            user.TotalXp += achievement.XpReward;
        }
    }
}
