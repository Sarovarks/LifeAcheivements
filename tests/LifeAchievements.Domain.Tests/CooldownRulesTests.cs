using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Domain.Tests;

public class CooldownRulesTests
{
    private static DateTimeOffset Local(int year, int month, int day, int hour = 12, int minute = 0)
    {
        var naive = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(naive, TimeZoneInfo.Local.GetUtcOffset(naive));
    }

    [Fact]
    public void None_never_cools_down() =>
        Assert.False(CooldownRules.IsStillCoolingDown(
            CooldownWindow.None, Local(2026, 3, 10), Local(2026, 3, 10)));

    [Fact]
    public void A_second_claim_on_the_same_day_is_blocked() =>
        Assert.True(CooldownRules.IsStillCoolingDown(
            CooldownWindow.Day, Local(2026, 3, 10, 8), Local(2026, 3, 10, 23, 59)));

    [Fact]
    public void The_next_morning_is_a_fresh_claim_even_after_a_late_one()
    {
        // This is the case the old rolling cooldown got wrong: logged at 9pm, walked again
        // the next morning, and a 24-hour wait would still have 13 hours to run.
        Assert.False(CooldownRules.IsStillCoolingDown(
            CooldownWindow.Day, Local(2026, 3, 10, 21), Local(2026, 3, 11, 8)));
    }

    [Fact]
    public void One_minute_past_midnight_already_counts_as_a_new_day() =>
        Assert.False(CooldownRules.IsStillCoolingDown(
            CooldownWindow.Day, Local(2026, 3, 10, 23, 59), Local(2026, 3, 11, 0, 1)));

    [Fact]
    public void A_weekly_achievement_is_blocked_for_the_rest_of_its_week()
    {
        // Monday 9th and Sunday 15th March 2026 are the same week.
        Assert.True(CooldownRules.IsStillCoolingDown(
            CooldownWindow.Week, Local(2026, 3, 9), Local(2026, 3, 15, 23)));
    }

    [Fact]
    public void A_weekly_achievement_frees_up_on_Monday() =>
        Assert.False(CooldownRules.IsStillCoolingDown(
            CooldownWindow.Week, Local(2026, 3, 9), Local(2026, 3, 16)));

    [Fact]
    public void NextClaimableAt_for_a_daily_achievement_is_the_next_midnight()
    {
        var next = CooldownRules.NextClaimableAt(CooldownWindow.Day, Local(2026, 3, 10, 21));

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 11), next!.Value.DateTime);
    }

    [Fact]
    public void NextClaimableAt_for_a_weekly_achievement_is_the_next_Monday()
    {
        var next = CooldownRules.NextClaimableAt(CooldownWindow.Week, Local(2026, 3, 11));

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 16), next!.Value.DateTime);
    }

    [Fact]
    public void NextClaimableAt_is_null_when_there_is_no_calendar_window() =>
        Assert.Null(CooldownRules.NextClaimableAt(CooldownWindow.None, Local(2026, 3, 10)));

    [Fact]
    public void A_daily_cooldown_never_blocks_a_day_the_streak_would_count()
    {
        // The streak counts distinct local days; the cooldown must free up on exactly those
        // boundaries, otherwise it can block a claim on a day you really did the thing.
        var firstClaim = Local(2026, 3, 10, 21);

        for (var day = 1; day <= 5; day++)
        {
            var nextDayEarly = Local(2026, 3, 10 + day, 6);
            Assert.False(CooldownRules.IsStillCoolingDown(CooldownWindow.Day, firstClaim, nextDayEarly));
        }
    }
}
