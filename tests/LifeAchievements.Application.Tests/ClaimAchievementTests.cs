using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

public class ClaimAchievementTests
{
    [Fact]
    public async Task Self_verified_claim_unlocks_immediately_and_pays_the_xp()
    {
        await using var h = new ClaimTestHarness();

        var result = await h.Claim("SHIPPED_IT");

        Assert.Equal(AchievementStatuses.Unlocked, result.Status);
        Assert.Equal(200, result.XpGained);
        // NewTotalXp is the player's total afterwards, so it also carries the First Steps bonus.
        Assert.Equal(250, result.NewTotalXp);

        var user = await h.ReloadUser();
        Assert.Equal(250, user.TotalXp);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task Every_xp_change_lands_in_the_ledger()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("SHIPPED_IT");

        var entries = await h.Db.XpTransactions.AsNoTracking()
            .Where(x => x.UserId == h.UserId).ToListAsync();

        // Shipped It plus the First Steps hidden achievement it triggers.
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e is { Amount: 200 } && e.Reason.Contains("Shipped It"));
        Assert.All(entries, e => Assert.NotNull(e.SourceUserAchievementId));
    }

    [Fact]
    public async Task A_non_repeatable_achievement_cannot_be_claimed_twice()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("SHIPPED_IT");

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("SHIPPED_IT"));
        Assert.Contains("already unlocked", ex.Message, StringComparison.OrdinalIgnoreCase);

        var user = await h.ReloadUser();
        Assert.Equal(await h.LedgerTotal(), user.TotalXp);
    }

    [Fact]
    public async Task A_repeatable_achievement_is_blocked_for_the_rest_of_its_window()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("EARLY_BIRD");

        // Early Bird is a daily habit, so the rest of the same day is off limits.
        h.Clock.Advance(TimeSpan.FromHours(5));
        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("EARLY_BIRD"));
        Assert.Contains("already logged", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_repeatable_achievement_can_be_claimed_again_once_the_cooldown_expires()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("EARLY_BIRD");
        h.Clock.Advance(TimeSpan.FromHours(24));
        var second = await h.Claim("EARLY_BIRD");

        Assert.Equal(AchievementStatuses.Unlocked, second.Status);
        // Early Bird (50) + the First Steps bonus (50) + Early Bird again (50).
        Assert.Equal(50, second.XpGained);
        Assert.Equal(150, second.NewTotalXp);

        var unlocks = await h.Db.UserAchievements.AsNoTracking()
            .CountAsync(ua => ua.UserId == h.UserId && ua.AchievementId == h.AchievementId("EARLY_BIRD"));
        Assert.Equal(2, unlocks);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task A_friend_verified_claim_stays_pending_and_pays_nothing_yet()
    {
        await using var h = new ClaimTestHarness();

        var result = await h.Claim("FIRST_DAY_FIRST_SHOW");

        Assert.Equal(AchievementStatuses.Pending, result.Status);
        Assert.Equal(0, result.XpGained);
        Assert.Equal(0, result.NewTotalXp);
        Assert.False(result.LeveledUp);

        var claim = await h.Db.UserAchievements.AsNoTracking()
            .SingleAsync(ua => ua.AchievementId == h.AchievementId("FIRST_DAY_FIRST_SHOW"));
        Assert.Equal(ClaimStatus.Pending, claim.Status);
        Assert.Null(claim.ApprovedAt);

        var user = await h.ReloadUser();
        Assert.Equal(0, user.TotalXp);
    }

    [Fact]
    public async Task The_same_pending_claim_cannot_be_submitted_twice()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("FIRST_DAY_FIRST_SHOW");

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("FIRST_DAY_FIRST_SHOW"));
        Assert.Contains("verify", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_hidden_achievement_cannot_be_claimed_by_hand()
    {
        await using var h = new ClaimTestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim(HiddenAchievements.NightOwl));
        Assert.Contains("unlocks itself", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Claiming_an_achievement_that_does_not_exist_is_a_friendly_error()
    {
        await using var h = new ClaimTestHarness();

        var handler = h.Handler();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new ClaimAchievementCommand(Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("does not exist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Crossing_a_threshold_reports_a_level_up()
    {
        await using var h = new ClaimTestHarness();

        // First Steps (50) + Half Marathon (500) = 550 XP, which is level 4.
        var result = await h.Claim("HALF_MARATHON");

        Assert.True(result.LeveledUp);
        Assert.Equal(1, result.OldLevel);
        Assert.Equal(LevelCurve.LevelFor(result.NewTotalXp), result.NewLevel);
        Assert.True(result.NewLevel > 1);
        Assert.Equal(LevelCurve.TitleFor(result.NewLevel), result.NewLevelTitle);
    }

    [Fact]
    public async Task A_claim_that_does_not_cross_a_threshold_does_not_report_a_level_up()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("HALF_MARATHON");           // gets us well clear of level 1
        var levelBefore = LevelCurve.LevelFor((await h.ReloadUser()).TotalXp);

        var result = await h.Claim("DEAR_DIARY"); // only 30 XP

        Assert.Equal(levelBefore, result.NewLevel);
        Assert.False(result.LeveledUp);
    }

    [Fact]
    public async Task The_result_carries_the_level_ring_numbers_the_unlock_screen_draws()
    {
        await using var h = new ClaimTestHarness();

        var result = await h.Claim("SHIPPED_IT");
        var progress = LevelCurve.ProgressFor(result.NewTotalXp);

        Assert.Equal(progress.XpToNextLevel, result.XpToNextLevel);
        Assert.Equal(progress.ProgressPercent, result.ProgressPercent, 3);
    }
}
