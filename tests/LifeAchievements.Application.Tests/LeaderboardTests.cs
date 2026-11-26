using LifeAchievements.Application.Friends.Queries;
using LifeAchievements.Shared.Dtos;

namespace LifeAchievements.Application.Tests;

public class LeaderboardTests
{
    /// <summary>Wednesday 11 March 2026, local - safely mid-week.</summary>
    private static DateTimeOffset Midweek(int hour = 12)
    {
        var naive = new DateTime(2026, 3, 11, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(naive, TimeZoneInfo.Local.GetUtcOffset(naive));
    }

    private static Task<WeeklyLeaderboardDto> Board(ClaimTestHarness h, Guid asUser) =>
        new GetWeeklyLeaderboardQueryHandler(h.Db, new TestCurrentUser(asUser), h.Clock)
            .Handle(new GetWeeklyLeaderboardQuery(), CancellationToken.None);

    [Fact]
    public async Task On_your_own_you_are_first_of_one()
    {
        await using var h = new ClaimTestHarness(Midweek());

        var board = await Board(h, h.UserId);

        var me = Assert.Single(board.Entries);
        Assert.Equal(1, me.Rank);
        Assert.True(me.IsMe);
        Assert.Equal(0, me.XpThisWeek);
    }

    [Fact]
    public async Task It_ranks_you_and_your_friends_by_xp_earned_this_week()
    {
        await using var h = new ClaimTestHarness(Midweek());
        var nidha = h.AddUser("Nidha", "nidha@example.com");
        var akhil = h.AddUser("Akhil", "akhil@example.com");
        h.MakeFriends(h.UserId, nidha);
        h.MakeFriends(h.UserId, akhil);

        await h.ClaimAs(nidha, "HALF_MARATHON");   // 500 + 50 bonus
        await h.ClaimAs(akhil, "DEAR_DIARY");      // 30 + 50 bonus
        await h.Claim("SOLO_TRIP");                // 400 + 50 bonus

        var board = await Board(h, h.UserId);

        Assert.Equal(3, board.Entries.Count);
        Assert.Equal("Nidha", board.Entries[0].DisplayName);
        Assert.Equal("Test Player", board.Entries[1].DisplayName);
        Assert.Equal("Akhil", board.Entries[2].DisplayName);
        Assert.Equal([1, 2, 3], board.Entries.Select(e => e.Rank));
    }

    [Fact]
    public async Task The_podium_is_the_top_three()
    {
        await using var h = new ClaimTestHarness(Midweek());
        foreach (var name in new[] { "A", "B", "C", "D" })
            h.MakeFriends(h.UserId, h.AddUser(name, $"{name.ToLower()}@example.com"));

        var board = await Board(h, h.UserId);

        Assert.Equal(5, board.Entries.Count);
        Assert.Equal(3, board.Podium.Count);
        Assert.NotNull(board.Me);
    }

    [Fact]
    public async Task Strangers_never_appear()
    {
        await using var h = new ClaimTestHarness(Midweek());
        var stranger = h.AddUser("Nobody", "nobody@example.com");
        await h.ClaimAs(stranger, "HALF_MARATHON");

        var board = await Board(h, h.UserId);

        Assert.DoesNotContain(board.Entries, e => e.DisplayName == "Nobody");
    }

    [Fact]
    public async Task Xp_earned_before_this_week_does_not_count()
    {
        await using var h = new ClaimTestHarness(Midweek());
        var nidha = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, nidha);

        // Last week's effort - real XP, but not this week's race.
        h.Clock.Now = Midweek().AddDays(-7);
        await h.ClaimAs(nidha, "HALF_MARATHON");

        h.Clock.Now = Midweek();
        await h.Claim("DEAR_DIARY");

        var board = await Board(h, h.UserId);

        Assert.Equal("Test Player", board.Entries[0].DisplayName);
        Assert.Equal(0, board.Entries.Single(e => e.DisplayName == "Nidha").XpThisWeek);

        // Lifetime XP still shows them as the higher level.
        Assert.True(board.Entries.Single(e => e.DisplayName == "Nidha").Level > 1);
    }

    [Fact]
    public async Task The_week_runs_Monday_to_Monday()
    {
        await using var h = new ClaimTestHarness(Midweek());

        var board = await Board(h, h.UserId);

        Assert.Equal(DayOfWeek.Monday, board.WeekStartedAt.LocalDateTime.DayOfWeek);
        Assert.Equal(new DateTime(2026, 3, 9), board.WeekStartedAt.LocalDateTime);
        Assert.Equal(new DateTime(2026, 3, 16), board.WeekEndsAt.LocalDateTime);
    }

    [Fact]
    public async Task It_counts_unlocks_as_well_as_xp()
    {
        await using var h = new ClaimTestHarness(Midweek());

        await h.Claim("DEAR_DIARY");
        await h.Claim("STILL_MIND");

        var me = (await Board(h, h.UserId)).Me;

        Assert.NotNull(me);
        // Two claims plus the First Steps the first one triggered.
        Assert.Equal(3, me!.UnlocksThisWeek);
    }
}
