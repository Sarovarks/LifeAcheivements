using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

public class HiddenAchievementTests
{
    /// <summary>A wall-clock local time, so the 2 AM rule is tested the way a player lives it.</summary>
    private static DateTimeOffset LocalTime(int hour, int minute = 0)
    {
        var local = new DateTime(2026, 3, 10, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static async Task<bool> HasUnlocked(ClaimTestHarness h, string code)
    {
        var id = h.AchievementId(code);
        return await h.Db.UserAchievements.AsNoTracking()
            .AnyAsync(ua => ua.UserId == h.UserId && ua.AchievementId == id && ua.Status == ClaimStatus.Approved);
    }

    [Fact]
    public async Task The_very_first_unlock_awards_First_Steps()
    {
        await using var h = new ClaimTestHarness();

        var result = await h.Claim("DEAR_DIARY");

        Assert.Contains(result.BonusUnlocks, b => b.Code == HiddenAchievements.FirstSteps);
        Assert.True(await HasUnlocked(h, HiddenAchievements.FirstSteps));

        // 30 for Dear Diary + 50 for First Steps, both through the ledger.
        Assert.Equal(80, (await h.ReloadUser()).TotalXp);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task First_Steps_is_only_ever_awarded_once()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("DEAR_DIARY");
        h.Clock.Advance(TimeSpan.FromHours(25));
        var second = await h.Claim("DEAR_DIARY");

        Assert.DoesNotContain(second.BonusUnlocks, b => b.Code == HiddenAchievements.FirstSteps);

        var id = h.AchievementId(HiddenAchievements.FirstSteps);
        var count = await h.Db.UserAchievements.AsNoTracking()
            .CountAsync(ua => ua.UserId == h.UserId && ua.AchievementId == id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Claiming_at_3am_awards_Night_Owl()
    {
        await using var h = new ClaimTestHarness(LocalTime(3, 15));

        var result = await h.Claim("DEEP_WORK");

        Assert.Contains(result.BonusUnlocks, b => b.Code == HiddenAchievements.NightOwl);
        Assert.True(await HasUnlocked(h, HiddenAchievements.NightOwl));
    }

    [Fact]
    public async Task Claiming_at_a_civilised_hour_does_not_award_Night_Owl()
    {
        await using var h = new ClaimTestHarness(LocalTime(14));

        var result = await h.Claim("DEEP_WORK");

        Assert.DoesNotContain(result.BonusUnlocks, b => b.Code == HiddenAchievements.NightOwl);
        Assert.False(await HasUnlocked(h, HiddenAchievements.NightOwl));
    }

    [Fact]
    public async Task Five_unlocks_in_one_day_awards_Overachiever()
    {
        await using var h = new ClaimTestHarness(LocalTime(9));

        await h.Claim("DEAR_DIARY");
        await h.Claim("STILL_MIND");
        await h.Claim("HOME_CHEF");
        await h.Claim("TEN_K_STEPS");
        Assert.False(await HasUnlocked(h, HiddenAchievements.Overachiever));

        var fifth = await h.Claim("SUNSET_CHASER");

        Assert.Contains(fifth.BonusUnlocks, b => b.Code == HiddenAchievements.Overachiever);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task One_unlock_in_every_category_awards_Well_Rounded()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("TEN_K_STEPS");   // Health
        await h.Claim("DEEP_WORK");     // Work
        await h.Claim("HOME_CHEF");     // Fun
        await h.Claim("FAMILY_TIME");   // Social
        Assert.False(await HasUnlocked(h, HiddenAchievements.WellRounded));

        var last = await h.Claim("STILL_MIND"); // Mind

        Assert.Contains(last.BonusUnlocks, b => b.Code == HiddenAchievements.WellRounded);
        Assert.True(await HasUnlocked(h, HiddenAchievements.WellRounded));
    }

    [Fact]
    public async Task Coming_back_after_two_quiet_weeks_awards_Comeback_Kid()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("DEAR_DIARY");
        h.Clock.Advance(TimeSpan.FromDays(20));
        var comeback = await h.Claim("STILL_MIND");

        Assert.Contains(comeback.BonusUnlocks, b => b.Code == HiddenAchievements.ComebackKid);
    }

    [Fact]
    public async Task A_short_break_does_not_award_Comeback_Kid()
    {
        await using var h = new ClaimTestHarness();

        await h.Claim("DEAR_DIARY");
        h.Clock.Advance(TimeSpan.FromDays(3));
        var next = await h.Claim("STILL_MIND");

        Assert.DoesNotContain(next.BonusUnlocks, b => b.Code == HiddenAchievements.ComebackKid);
    }

    [Fact]
    public async Task The_thirtieth_unlock_of_one_repeatable_awards_Deja_Vu()
    {
        await using var h = new ClaimTestHarness();

        for (var i = 0; i < HiddenAchievements.DejaVuRepeatCount - 1; i++)
        {
            await h.Claim("DEAR_DIARY");
            h.Clock.Advance(TimeSpan.FromHours(25));
        }

        Assert.False(await HasUnlocked(h, HiddenAchievements.DejaVu));

        var thirtieth = await h.Claim("DEAR_DIARY");

        Assert.Contains(thirtieth.BonusUnlocks, b => b.Code == HiddenAchievements.DejaVu);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task Bonus_xp_goes_through_the_ledger_like_everything_else()
    {
        await using var h = new ClaimTestHarness(LocalTime(3));

        // Awards Deep Work (70), First Steps (50) and Night Owl (100) all at once.
        var result = await h.Claim("DEEP_WORK");

        Assert.Equal(2, result.BonusUnlocks.Count);
        Assert.Equal(220, (await h.ReloadUser()).TotalXp);
        await h.AssertLedgerMatchesCachedTotal();
    }
}
