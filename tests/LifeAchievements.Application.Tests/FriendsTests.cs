using LifeAchievements.Application.Friends.Commands;
using LifeAchievements.Application.Friends.Queries;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

public class FriendsTests
{
    private static SendFriendRequestCommandHandler Send(ClaimTestHarness h, Guid asUser) =>
        new(h.Db, new TestCurrentUser(asUser), h.Clock);

    private static RespondToFriendRequestCommandHandler Respond(ClaimTestHarness h, Guid asUser) =>
        new(h.Db, new TestCurrentUser(asUser), h.Clock);

    private static Task<FriendsOverviewDto> Overview(ClaimTestHarness h, Guid asUser) =>
        new GetFriendsQueryHandler(h.Db, new TestCurrentUser(asUser), h.Clock)
            .Handle(new GetFriendsQuery(), CancellationToken.None);

    [Fact]
    public async Task A_request_can_be_sent_by_email_and_accepted()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");

        var id = await Send(h, h.UserId).Handle(
            new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);

        // Until it is accepted, neither side has a friend.
        Assert.Empty((await Overview(h, h.UserId)).Friends);

        var incoming = (await Overview(h, friend)).Requests.Single();
        Assert.Equal(RequestDirections.Incoming, incoming.Direction);
        Assert.Equal("Test Player", incoming.DisplayName);

        await Respond(h, friend).Handle(new RespondToFriendRequestCommand(id, true), CancellationToken.None);

        Assert.Equal("Nidha", (await Overview(h, h.UserId)).Friends.Single().DisplayName);
        Assert.Equal("Test Player", (await Overview(h, friend)).Friends.Single().DisplayName);
    }

    [Fact]
    public async Task A_request_shows_as_outgoing_to_the_sender()
    {
        await using var h = new ClaimTestHarness();
        h.AddUser("Nidha", "nidha@example.com");

        await Send(h, h.UserId).Handle(new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);

        var request = (await Overview(h, h.UserId)).Requests.Single();
        Assert.Equal(RequestDirections.Outgoing, request.Direction);
    }

    [Fact]
    public async Task An_unknown_email_is_a_friendly_error()
    {
        await using var h = new ClaimTestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Send(h, h.UserId).Handle(new SendFriendRequestCommand("nobody@example.com"), CancellationToken.None));

        Assert.Contains("Nobody is playing", ex.Message);
    }

    [Fact]
    public async Task You_cannot_befriend_yourself()
    {
        await using var h = new ClaimTestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Send(h, h.UserId).Handle(new SendFriendRequestCommand("player@example.com"), CancellationToken.None));

        Assert.Contains("cannot add yourself", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Asking_twice_is_refused()
    {
        await using var h = new ClaimTestHarness();
        h.AddUser("Nidha", "nidha@example.com");

        await Send(h, h.UserId).Handle(new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Send(h, h.UserId).Handle(new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None));

        Assert.Contains("already asked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Crossing_requests_become_a_friendship_rather_than_a_second_row()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");

        await Send(h, h.UserId).Handle(new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);
        // They ask back before answering - that is a yes, not a duplicate.
        await Send(h, friend).Handle(new SendFriendRequestCommand("player@example.com"), CancellationToken.None);

        Assert.Equal(1, await h.Db.Friendships.CountAsync());
        Assert.Single((await Overview(h, h.UserId)).Friends);
    }

    [Fact]
    public async Task Only_the_person_asked_can_accept()
    {
        await using var h = new ClaimTestHarness();
        h.AddUser("Nidha", "nidha@example.com");

        var id = await Send(h, h.UserId).Handle(
            new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Respond(h, h.UserId).Handle(new RespondToFriendRequestCommand(id, true), CancellationToken.None));

        Assert.Contains("not yours to answer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_declined_request_leaves_no_friendship_but_can_be_sent_again()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");

        var id = await Send(h, h.UserId).Handle(
            new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);
        await Respond(h, friend).Handle(new RespondToFriendRequestCommand(id, false), CancellationToken.None);

        Assert.Empty((await Overview(h, h.UserId)).Friends);
        Assert.Empty((await Overview(h, h.UserId)).Requests);

        // A no is not forever.
        await Send(h, h.UserId).Handle(new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);
        Assert.Single((await Overview(h, friend)).Requests);
        Assert.Equal(1, await h.Db.Friendships.CountAsync());
    }

    [Fact]
    public async Task A_request_cannot_be_answered_twice()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");

        var id = await Send(h, h.UserId).Handle(
            new SendFriendRequestCommand("nidha@example.com"), CancellationToken.None);
        await Respond(h, friend).Handle(new RespondToFriendRequestCommand(id, true), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Respond(h, friend).Handle(new RespondToFriendRequestCommand(id, true), CancellationToken.None));

        Assert.Contains("already been dealt with", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_friend_carries_their_level_and_streak()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        await h.ClaimAs(friend, "HALF_MARATHON");

        var dto = (await Overview(h, h.UserId)).Friends.Single();
        Assert.Equal("Nidha", dto.DisplayName);
        Assert.True(dto.TotalXp > 0);
        Assert.True(dto.Level > 1);
        Assert.Equal(1, dto.DayStreak);
    }

    [Fact]
    public async Task Removing_a_friend_undoes_it_for_both_sides()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        await new RemoveFriendCommandHandler(h.Db, new TestCurrentUser(h.UserId))
            .Handle(new RemoveFriendCommand(friend), CancellationToken.None);

        Assert.Empty((await Overview(h, h.UserId)).Friends);
        Assert.Empty((await Overview(h, friend)).Friends);
    }
}
