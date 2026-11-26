using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Shared.Dtos;

namespace LifeAchievements.Application.Tests;

/// <summary>
/// The daily repeatables - 10K Steps and friends - go through the calendar-day rule, so the
/// claim window lines up with the day the player actually lived.
/// </summary>
public class DailyCooldownTests
{
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute = 0)
    {
        var naive = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(naive, TimeZoneInfo.Local.GetUtcOffset(naive));
    }

    [Fact]
    public async Task A_daily_achievement_cannot_be_claimed_twice_in_one_day()
    {
        await using var h = new ClaimTestHarness(Local(2026, 3, 10, 8));

        await h.Claim("TEN_K_STEPS");
        h.Clock.Now = Local(2026, 3, 10, 22);

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("TEN_K_STEPS"));
        Assert.Contains("already logged", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("today", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_evening_claim_does_not_block_the_next_morning()
    {
        // The bug this rule replaced: claim at 9pm, walk again at 8am, and a rolling
        // 24-hour cooldown would still refuse with 13 hours left to run.
        await using var h = new ClaimTestHarness(Local(2026, 3, 10, 21));

        await h.Claim("TEN_K_STEPS");
        h.Clock.Now = Local(2026, 3, 11, 8);

        var second = await h.Claim("TEN_K_STEPS");

        Assert.Equal(AchievementStatuses.Unlocked, second.Status);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task Seven_days_running_gives_seven_unlocks_and_a_seven_day_streak()
    {
        await using var h = new ClaimTestHarness(Local(2026, 3, 9, 20));

        for (var day = 0; day < 7; day++)
        {
            // Deliberately ragged times - the kind of week a real person has.
            h.Clock.Now = Local(2026, 3, 9 + day, day % 2 == 0 ? 7 : 22);
            await h.Claim("TEN_K_STEPS");
        }

        var dto = await FetchAsync(h, "TEN_K_STEPS");
        Assert.Equal(7, dto.TimesUnlocked);

        var me = await MeAsync(h);
        Assert.Equal(7, me.DayStreak);
    }

    [Fact]
    public async Task The_cooldown_and_the_streak_agree_on_where_a_day_ends()
    {
        await using var h = new ClaimTestHarness(Local(2026, 3, 10, 23, 50));

        await h.Claim("TEN_K_STEPS");

        // Ten minutes later, but a new calendar day: claimable, and the streak counts two days.
        h.Clock.Now = Local(2026, 3, 11, 0, 5);
        await h.Claim("TEN_K_STEPS");

        var me = await MeAsync(h);
        Assert.Equal(2, me.DayStreak);
    }

    [Fact]
    public async Task A_weekly_achievement_is_blocked_for_the_rest_of_the_week()
    {
        await using var h = new ClaimTestHarness(Local(2026, 3, 9, 10));   // Monday

        await h.Claim("INBOX_ZERO");

        h.Clock.Now = Local(2026, 3, 15, 23);                              // Sunday
        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("INBOX_ZERO"));
        Assert.Contains("this week", ex.Message, StringComparison.OrdinalIgnoreCase);

        h.Clock.Now = Local(2026, 3, 16, 9);                               // Monday again
        var next = await h.Claim("INBOX_ZERO");
        Assert.Equal(AchievementStatuses.Unlocked, next.Status);
    }

    [Fact]
    public async Task A_rolling_cooldown_still_applies_where_the_gap_is_the_point()
    {
        // Leg Day Survivor keeps its 72-hour wait - three days of recovery, not "a new day".
        await using var h = new ClaimTestHarness(Local(2026, 3, 10, 9));

        await h.Claim("LEG_DAY");

        h.Clock.Now = Local(2026, 3, 11, 9);
        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("LEG_DAY"));
        Assert.Contains("cooling down", ex.Message, StringComparison.OrdinalIgnoreCase);

        h.Clock.Now = Local(2026, 3, 13, 10);
        var next = await h.Claim("LEG_DAY");
        Assert.Equal(AchievementStatuses.Unlocked, next.Status);
    }

    [Fact]
    public async Task The_list_reports_when_a_daily_achievement_frees_up_again()
    {
        await using var h = new ClaimTestHarness(Local(2026, 3, 10, 21));

        await h.Claim("TEN_K_STEPS");
        var dto = await FetchAsync(h, "TEN_K_STEPS");

        Assert.Equal(AchievementStatuses.Cooldown, dto.Status);
        Assert.NotNull(dto.ClaimableAgainAt);

        // Midnight tonight, not 9pm tomorrow.
        Assert.Equal(new DateTime(2026, 3, 11), dto.ClaimableAgainAt!.Value.LocalDateTime);
    }

    [Fact]
    public async Task A_daily_achievement_reads_as_claimable_again_the_next_day()
    {
        await using var h = new ClaimTestHarness(Local(2026, 3, 10, 21));

        await h.Claim("TEN_K_STEPS");
        h.Clock.Now = Local(2026, 3, 11, 7);

        var dto = await FetchAsync(h, "TEN_K_STEPS");
        Assert.Equal(AchievementStatuses.Claimable, dto.Status);
    }

    private static async Task<AchievementDto> FetchAsync(ClaimTestHarness h, string code)
    {
        var handler = new Achievements.Queries.GetAchievementsQueryHandler(
            h.Db, new TestCurrentUser(h.UserId), h.Clock);
        var all = await handler.Handle(new Achievements.Queries.GetAchievementsQuery(null), CancellationToken.None);
        return all.Single(a => a.Code == code);
    }

    private static Task<MeDto> MeAsync(ClaimTestHarness h) =>
        new Users.Queries.GetMeQueryHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock)
            .Handle(new Users.Queries.GetMeQuery(), CancellationToken.None);
}
