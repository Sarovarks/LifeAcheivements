using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Domain.Tests;

public class StreakCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 19, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset DaysAgo(int days) => Now.AddDays(-days);

    [Fact]
    public void No_unlocks_means_no_streak() =>
        Assert.Equal(0, StreakCalculator.Calculate([], Now));

    [Fact]
    public void One_unlock_today_is_a_one_day_streak() =>
        Assert.Equal(1, StreakCalculator.Calculate([Now], Now));

    [Fact]
    public void Consecutive_days_count_up()
    {
        var unlocks = new[] { DaysAgo(0), DaysAgo(1), DaysAgo(2), DaysAgo(3) };
        Assert.Equal(4, StreakCalculator.Calculate(unlocks, Now));
    }

    [Fact]
    public void Several_unlocks_on_one_day_still_count_as_one_day()
    {
        var unlocks = new[] { Now, Now.AddHours(-2), Now.AddHours(-5), DaysAgo(1) };
        Assert.Equal(2, StreakCalculator.Calculate(unlocks, Now));
    }

    [Fact]
    public void A_gap_ends_the_streak()
    {
        var unlocks = new[] { DaysAgo(0), DaysAgo(1), DaysAgo(3), DaysAgo(4) };
        Assert.Equal(2, StreakCalculator.Calculate(unlocks, Now));
    }

    [Fact]
    public void Yesterday_still_counts_so_the_streak_does_not_die_at_midnight()
    {
        var unlocks = new[] { DaysAgo(1), DaysAgo(2) };
        Assert.Equal(2, StreakCalculator.Calculate(unlocks, Now));
    }

    [Fact]
    public void Nothing_for_two_days_resets_the_streak()
    {
        var unlocks = new[] { DaysAgo(2), DaysAgo(3), DaysAgo(4) };
        Assert.Equal(0, StreakCalculator.Calculate(unlocks, Now));
    }
}
