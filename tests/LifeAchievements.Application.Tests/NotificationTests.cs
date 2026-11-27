using LifeAchievements.Application.Friends.Commands;
using LifeAchievements.Application.Friends.Queries;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

public class NotificationTests
{
    private static Task<NotificationsDto> Notifications(ClaimTestHarness h, Guid asUser) =>
        new GetNotificationsQueryHandler(h.Db, new TestCurrentUser(asUser))
            .Handle(new GetNotificationsQuery(), CancellationToken.None);

    private static Task<bool> MarkSeen(ClaimTestHarness h, Guid asUser) =>
        new MarkNotificationsSeenCommandHandler(h.Db, new TestCurrentUser(asUser), h.Clock)
            .Handle(new MarkNotificationsSeenCommand(), CancellationToken.None);

    [Fact]
    public async Task A_cheer_from_a_friend_shows_up()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var mine = await h.Claim("SOLO_TRIP");

        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(mine.UserAchievementId, "Cheer"), CancellationToken.None);

        var result = await Notifications(h, h.UserId);

        var item = Assert.Single(result.Items, i => i.Kind == "Reaction");
        Assert.Equal("Nidha cheered you", item.Title);
        Assert.Equal("Solo Trip", item.Detail);
        Assert.True(item.IsNew);
        Assert.Equal(1, result.NewCount);
    }

    [Fact]
    public async Task The_friend_who_cheered_is_not_told_about_their_own_cheer()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var mine = await h.Claim("SOLO_TRIP");
        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(mine.UserAchievementId, "Cheer"), CancellationToken.None);

        var theirs = await Notifications(h, friend);

        Assert.DoesNotContain(theirs.Items, i => i.Kind == "Reaction");
    }

    [Fact]
    public async Task Reactions_left_on_your_own_unlock_before_the_rule_existed_are_not_reported()
    {
        await using var h = new ClaimTestHarness();
        var mine = await h.Claim("SOLO_TRIP");

        // The handler refuses this now, but rows like it are already in the live database
        // from before that rule - nobody needs telling that they cheered themselves.
        h.Db.Reactions.Add(new Domain.Entities.Reaction
        {
            UserAchievementId = mine.UserAchievementId,
            UserId = h.UserId,
            Type = ReactionType.Cheer,
            CreatedAt = h.Clock.Now
        });
        await h.Db.SaveChangesAsync(CancellationToken.None);

        var result = await Notifications(h, h.UserId);

        Assert.DoesNotContain(result.Items, i => i.Kind == "Reaction");
        Assert.Equal(0, result.NewCount);
    }

    [Fact]
    public async Task A_friend_request_shows_up()
    {
        await using var h = new ClaimTestHarness();
        var stranger = h.AddUser("Arun", "arun@example.com");

        await new SendFriendRequestCommandHandler(h.Db, new TestCurrentUser(stranger), h.Clock)
            .Handle(new SendFriendRequestCommand(h.UserEmail), CancellationToken.None);

        var result = await Notifications(h, h.UserId);

        var item = Assert.Single(result.Items, i => i.Kind == "FriendRequest");
        Assert.Equal("Arun wants to be friends", item.Title);
        Assert.NotNull(item.FriendshipId);
    }

    [Fact]
    public async Task A_claim_waiting_on_my_word_shows_up()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        // Friend-verified, so it parks as Pending and waits for me.
        await h.ClaimAs(friend, "FIRST_DAY_FIRST_SHOW");

        var result = await Notifications(h, h.UserId);

        var item = Assert.Single(result.Items, i => i.Kind == "Verification");
        Assert.Equal("Nidha needs you to vouch", item.Title);
    }

    [Fact]
    public async Task Opening_the_bell_clears_the_badge_without_emptying_the_list()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var mine = await h.Claim("SOLO_TRIP");
        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(mine.UserAchievementId, "Cheer"), CancellationToken.None);

        Assert.Equal(1, (await Notifications(h, h.UserId)).NewCount);

        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await MarkSeen(h, h.UserId);
        h.Db.ChangeTracker.Clear();

        var after = await Notifications(h, h.UserId);
        Assert.Equal(0, after.NewCount);
        Assert.Single(after.Items);            // still there to read, just not new
        Assert.False(after.Items[0].IsNew);
    }

    [Fact]
    public async Task Something_that_happens_after_you_looked_is_new_again()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var first = await h.Claim("SOLO_TRIP");
        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(first.UserAchievementId, "Cheer"), CancellationToken.None);

        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await MarkSeen(h, h.UserId);
        h.Db.ChangeTracker.Clear();

        h.Clock.Advance(TimeSpan.FromMinutes(5));
        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(first.UserAchievementId, "Respect"), CancellationToken.None);

        var result = await Notifications(h, h.UserId);

        Assert.Equal(1, result.NewCount);
        Assert.Equal("Nidha respects that", result.Items.First(i => i.IsNew).Title);
    }

    [Fact]
    public async Task A_brand_new_account_has_nothing_to_see()
    {
        await using var h = new ClaimTestHarness();

        var result = await Notifications(h, h.UserId);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.NewCount);
    }

    [Fact]
    public async Task Newest_first()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var mine = await h.Claim("SOLO_TRIP");
        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(mine.UserAchievementId, "Cheer"), CancellationToken.None);

        h.Clock.Advance(TimeSpan.FromHours(1));
        var stranger = h.AddUser("Arun", "arun@example.com");
        await new SendFriendRequestCommandHandler(h.Db, new TestCurrentUser(stranger), h.Clock)
            .Handle(new SendFriendRequestCommand(h.UserEmail), CancellationToken.None);

        var result = await Notifications(h, h.UserId);

        Assert.Equal("FriendRequest", result.Items[0].Kind);
        Assert.Equal("Reaction", result.Items[1].Kind);
    }
}
