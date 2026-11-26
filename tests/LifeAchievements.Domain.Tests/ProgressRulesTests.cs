using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Domain.Tests;

public class ProgressRulesTests
{
    // Wednesday 11 March 2026, in local time so week boundaries behave like a player's week.
    private static DateTimeOffset Local(int year, int month, int day, int hour = 12)
    {
        var naive = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(naive, TimeZoneInfo.Local.GetUtcOffset(naive));
    }

    [Fact]
    public void Forever_never_lapses() =>
        Assert.False(ProgressRules.HasWindowLapsed(
            ProgressWindow.Forever, Local(2020, 1, 1), Local(2030, 1, 1)));

    [Fact]
    public void A_week_does_not_lapse_within_the_same_week()
    {
        // Monday 9th to Sunday 15th March 2026 is one week.
        Assert.False(ProgressRules.HasWindowLapsed(
            ProgressWindow.Week, Local(2026, 3, 9), Local(2026, 3, 15, 23)));
    }

    [Fact]
    public void A_week_lapses_once_the_next_Monday_arrives()
    {
        Assert.True(ProgressRules.HasWindowLapsed(
            ProgressWindow.Week, Local(2026, 3, 9), Local(2026, 3, 16)));
    }

    [Fact]
    public void A_month_lapses_on_the_first_of_the_next_month()
    {
        Assert.False(ProgressRules.HasWindowLapsed(
            ProgressWindow.Month, Local(2026, 3, 1), Local(2026, 3, 31)));
        Assert.True(ProgressRules.HasWindowLapsed(
            ProgressWindow.Month, Local(2026, 3, 31), Local(2026, 4, 1)));
    }

    [Fact]
    public void A_year_lapses_on_the_first_of_January()
    {
        Assert.False(ProgressRules.HasWindowLapsed(
            ProgressWindow.Year, Local(2026, 1, 1), Local(2026, 12, 31)));
        Assert.True(ProgressRules.HasWindowLapsed(
            ProgressWindow.Year, Local(2026, 12, 31), Local(2027, 1, 1)));
    }

    [Fact]
    public void A_clock_going_backwards_does_not_wipe_a_run() =>
        Assert.False(ProgressRules.HasWindowLapsed(
            ProgressWindow.Week, Local(2026, 3, 16), Local(2026, 3, 9)));

    [Fact]
    public void StartOfWeek_is_Monday()
    {
        // 15 March 2026 is a Sunday; its week starts on Monday the 9th.
        Assert.Equal(new DateTime(2026, 3, 9), ProgressRules.StartOfWeek(new DateTime(2026, 3, 15)));
        Assert.Equal(new DateTime(2026, 3, 9), ProgressRules.StartOfWeek(new DateTime(2026, 3, 9)));
    }

    [Fact]
    public void Logging_adds_up_but_never_passes_the_target()
    {
        var progress = new AchievementProgress { WindowStartedAt = Local(2026, 3, 9) };

        progress.Log(3, target: 5, Local(2026, 3, 10));
        Assert.Equal(3, progress.Current);
        Assert.False(progress.IsComplete(5));

        progress.Log(10, target: 5, Local(2026, 3, 11));
        Assert.Equal(5, progress.Current);
        Assert.True(progress.IsComplete(5));
    }

    [Fact]
    public void The_window_restarts_from_the_first_logged_step()
    {
        var created = Local(2026, 3, 9);
        var progress = new AchievementProgress { Current = 0, WindowStartedAt = created };

        var firstStep = Local(2026, 3, 11);
        progress.Log(1, target: 5, firstStep);

        Assert.Equal(firstStep, progress.WindowStartedAt);
    }

    [Fact]
    public void A_lapsed_run_resets_to_zero()
    {
        var progress = new AchievementProgress { Current = 4, WindowStartedAt = Local(2026, 3, 9) };

        var reset = progress.ResetIfWindowLapsed(ProgressWindow.Week, Local(2026, 3, 16));

        Assert.True(reset);
        Assert.Equal(0, progress.Current);
    }

    [Fact]
    public void A_live_run_is_left_alone()
    {
        var progress = new AchievementProgress { Current = 4, WindowStartedAt = Local(2026, 3, 9) };

        var reset = progress.ResetIfWindowLapsed(ProgressWindow.Week, Local(2026, 3, 12));

        Assert.False(reset);
        Assert.Equal(4, progress.Current);
    }

    [Fact]
    public void WindowEndsAt_is_the_next_Monday_for_a_weekly_run()
    {
        var ends = ProgressRules.WindowEndsAt(ProgressWindow.Week, Local(2026, 3, 11));

        Assert.NotNull(ends);
        Assert.Equal(new DateTime(2026, 3, 16), ends!.Value.DateTime);
    }

    [Fact]
    public void WindowEndsAt_is_null_when_a_run_never_expires() =>
        Assert.Null(ProgressRules.WindowEndsAt(ProgressWindow.Forever, Local(2026, 3, 11)));
}
