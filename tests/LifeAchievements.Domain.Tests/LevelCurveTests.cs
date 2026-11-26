using LifeAchievements.Domain.Rules;

namespace LifeAchievements.Domain.Tests;

public class LevelCurveTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(149, 1)]
    [InlineData(150, 2)]
    [InlineData(349, 2)]
    [InlineData(350, 3)]
    [InlineData(900, 5)]
    [InlineData(3899, 9)]
    [InlineData(3900, 10)]
    [InlineData(17400, 20)]
    public void LevelFor_matches_the_threshold_table(int totalXp, int expectedLevel) =>
        Assert.Equal(expectedLevel, LevelCurve.LevelFor(totalXp));

    [Fact]
    public void Negative_xp_still_lands_on_level_one() =>
        Assert.Equal(1, LevelCurve.LevelFor(-500));

    [Theory]
    [InlineData(19400, 21)]
    [InlineData(21400, 22)]
    [InlineData(37400, 30)]
    public void Past_the_table_the_curve_keeps_climbing(int totalXp, int expectedLevel) =>
        Assert.Equal(expectedLevel, LevelCurve.LevelFor(totalXp));

    [Fact]
    public void XpRequiredFor_and_LevelFor_agree_at_every_boundary()
    {
        for (var level = 1; level <= 40; level++)
        {
            var floor = LevelCurve.XpRequiredFor(level);
            Assert.Equal(level, LevelCurve.LevelFor(floor));
            if (level > 1)
                Assert.Equal(level - 1, LevelCurve.LevelFor(floor - 1));
        }
    }

    [Fact]
    public void XpRequiredFor_is_strictly_increasing()
    {
        for (var level = 1; level < 40; level++)
            Assert.True(LevelCurve.XpRequiredFor(level + 1) > LevelCurve.XpRequiredFor(level));
    }

    [Theory]
    [InlineData(1, "NPC")]
    [InlineData(2, "NPC")]
    [InlineData(5, "Side Character")]
    [InlineData(10, "Main Character")]
    [InlineData(20, "Final Boss")]
    [InlineData(99, "Final Boss")]
    public void TitleFor_uses_the_agreed_bands(int level, string expected) =>
        Assert.Equal(expected, LevelCurve.TitleFor(level));

    [Fact]
    public void ProgressFor_reports_the_gap_to_the_next_level()
    {
        var progress = LevelCurve.ProgressFor(200);

        Assert.Equal(2, progress.Level);
        Assert.Equal("NPC", progress.Title);
        Assert.Equal(150, progress.CurrentLevelFloorXp);
        Assert.Equal(350, progress.NextLevelAtXp);
        Assert.Equal(150, progress.XpToNextLevel);
        Assert.Equal(25d, progress.ProgressPercent, 3);
    }

    [Fact]
    public void ProgressFor_is_zero_percent_exactly_on_a_level_boundary()
    {
        var progress = LevelCurve.ProgressFor(LevelCurve.XpRequiredFor(7));

        Assert.Equal(7, progress.Level);
        Assert.Equal(0d, progress.ProgressPercent, 3);
    }

    [Fact]
    public void ProgressPercent_never_leaves_the_zero_to_hundred_range()
    {
        foreach (var xp in new[] { 0, 1, 149, 150, 3899, 17400, 50_000 })
        {
            var percent = LevelCurve.ProgressFor(xp).ProgressPercent;
            Assert.InRange(percent, 0d, 100d);
        }
    }
}
