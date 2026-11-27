using LifeAchievements.Application.Friends.Commands;
using LifeAchievements.Application.Friends.Queries;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Domain.Rules;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

public class VerificationAndFeedTests
{
    private static VerifyClaimCommandHandler Verifier(ClaimTestHarness h, Guid asUser) =>
        new(h.Db, new TestCurrentUser(asUser), h.Clock, h.Unlocks());

    private static Task<IReadOnlyList<PendingVerificationDto>> Pending(ClaimTestHarness h, Guid asUser) =>
        new GetPendingVerificationsQueryHandler(h.Db, new TestCurrentUser(asUser))
            .Handle(new GetPendingVerificationsQuery(), CancellationToken.None);

    private static Task<IReadOnlyList<FeedItemDto>> Feed(ClaimTestHarness h, Guid asUser) =>
        new GetFeedQueryHandler(h.Db, new TestCurrentUser(asUser))
            .Handle(new GetFeedQuery(30), CancellationToken.None);

    // ------------------------------------------------------------ verification --

    [Fact]
    public async Task A_friend_verifying_a_claim_finally_pays_the_xp()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        // Friend-verified claims have been parking as Pending since Phase 1.
        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");
        Assert.Equal(AchievementStatuses.Pending, claim.Status);
        Assert.Equal(0, (await h.ReloadUser()).TotalXp);

        var result = await Verifier(h, friend).Handle(
            new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None);

        Assert.True(result.Approved);
        Assert.Equal(75, result.XpAwarded);

        var me = await h.ReloadUser();
        Assert.Equal(125, me.TotalXp);   // 75 for the film, 50 for the First Steps bonus
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task Verification_runs_the_hidden_rules_like_any_other_unlock()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");
        await Verifier(h, friend).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None);

        var firstSteps = h.AchievementId(HiddenAchievements.FirstSteps);
        Assert.True(await h.Db.UserAchievements.AnyAsync(ua =>
            ua.UserId == h.UserId && ua.AchievementId == firstSteps && ua.Status == ClaimStatus.Approved));
    }

    [Fact]
    public async Task Verification_records_who_vouched_for_it()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");
        await Verifier(h, friend).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None);

        var row = await h.Db.UserAchievements.AsNoTracking()
            .SingleAsync(ua => ua.Id == claim.UserAchievementId);

        Assert.Equal(ClaimStatus.Approved, row.Status);
        Assert.Equal(friend, row.VerifiedByUserId);
    }

    [Fact]
    public async Task A_rejected_claim_pays_nothing()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");
        var result = await Verifier(h, friend).Handle(
            new VerifyClaimCommand(claim.UserAchievementId, false), CancellationToken.None);

        Assert.False(result.Approved);
        Assert.Equal(0, result.XpAwarded);
        Assert.Equal(0, (await h.ReloadUser()).TotalXp);

        var row = await h.Db.UserAchievements.AsNoTracking().SingleAsync(ua => ua.Id == claim.UserAchievementId);
        Assert.Equal(ClaimStatus.Rejected, row.Status);
    }

    [Fact]
    public async Task You_cannot_verify_your_own_claim()
    {
        await using var h = new ClaimTestHarness();
        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Verifier(h, h.UserId).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None));

        Assert.Contains("your own claim", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_stranger_cannot_verify_a_claim()
    {
        await using var h = new ClaimTestHarness();
        var stranger = h.AddUser("Nobody", "nobody@example.com");

        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Verifier(h, stranger).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None));

        Assert.Contains("only verify a friend", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, (await h.ReloadUser()).TotalXp);
    }

    [Fact]
    public async Task A_claim_cannot_be_verified_twice()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var claim = await h.Claim("FIRST_DAY_FIRST_SHOW");
        await Verifier(h, friend).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Verifier(h, friend).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None));

        Assert.Contains("already been settled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pending_verifications_list_only_friends_claims()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        var stranger = h.AddUser("Nobody", "nobody@example.com");
        h.MakeFriends(h.UserId, friend);

        await h.ClaimAs(friend, "FIRST_DAY_FIRST_SHOW");
        await h.ClaimAs(stranger, "FIRST_DAY_FIRST_SHOW");

        var waiting = await Pending(h, h.UserId);

        Assert.Single(waiting);
        Assert.Equal("Nidha", waiting[0].DisplayName);
    }

    [Fact]
    public async Task A_verified_claim_leaves_the_pending_list()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var claim = await h.ClaimAs(friend, "FIRST_DAY_FIRST_SHOW");
        Assert.Single(await Pending(h, h.UserId));

        await Verifier(h, h.UserId).Handle(new VerifyClaimCommand(claim.UserAchievementId, true), CancellationToken.None);

        Assert.Empty(await Pending(h, h.UserId));
    }

    // -------------------------------------------------------------------- feed --

    [Fact]
    public async Task The_feed_shows_friends_unlocks_and_your_own()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        var stranger = h.AddUser("Nobody", "nobody@example.com");
        h.MakeFriends(h.UserId, friend);

        await h.Claim("SOLO_TRIP");
        await h.ClaimAs(friend, "HALF_MARATHON");
        await h.ClaimAs(stranger, "MIC_DROP");

        var feed = await Feed(h, h.UserId);

        Assert.Contains(feed, f => f.AchievementName == "Solo Trip" && f.IsMine);
        Assert.Contains(feed, f => f.AchievementName == "Half Marathon" && f.DisplayName == "Nidha");
        Assert.DoesNotContain(feed, f => f.DisplayName == "Nobody");
    }

    [Fact]
    public async Task The_feed_never_leaks_hidden_achievements()
    {
        await using var h = new ClaimTestHarness();

        // Claiming anything triggers the hidden First Steps.
        await h.Claim("SOLO_TRIP");

        var feed = await Feed(h, h.UserId);
        Assert.DoesNotContain(feed, f => f.AchievementName == "First Steps");
    }

    [Fact]
    public async Task The_feed_carries_the_note()
    {
        await using var h = new ClaimTestHarness();

        var unlock = await h.Claim("SOLO_TRIP");
        await new Achievements.Commands.SetUnlockNoteCommandHandler(h.Db, new TestCurrentUser(h.UserId))
            .Handle(new Achievements.Commands.SetUnlockNoteCommand(unlock.UserAchievementId, "Munnar."),
                CancellationToken.None);

        var item = (await Feed(h, h.UserId)).Single(f => f.AchievementName == "Solo Trip");
        Assert.Equal("Munnar.", item.Note);
    }

    // --------------------------------------------------------------- reactions --

    [Fact]
    public async Task A_cheer_can_be_given_and_taken_back()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var unlock = await h.ClaimAs(friend, "HALF_MARATHON");
        var toggle = new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock);

        var on = await toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Cheer"), CancellationToken.None);
        Assert.Equal(1, on.Cheers);
        Assert.True(on.ICheered);

        var off = await toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Cheer"), CancellationToken.None);
        Assert.Equal(0, off.Cheers);
        Assert.False(off.ICheered);
    }

    [Fact]
    public async Task Cheer_and_Respect_are_independent()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var unlock = await h.ClaimAs(friend, "HALF_MARATHON");
        var toggle = new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock);

        await toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Cheer"), CancellationToken.None);
        var both = await toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Respect"), CancellationToken.None);

        Assert.Equal(1, both.Cheers);
        Assert.Equal(1, both.Respects);
    }

    [Fact]
    public async Task You_cannot_react_to_a_strangers_unlock()
    {
        await using var h = new ClaimTestHarness();
        var stranger = h.AddUser("Nobody", "nobody@example.com");

        var unlock = await h.ClaimAs(stranger, "HALF_MARATHON");
        var toggle = new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Cheer"), CancellationToken.None));

        Assert.Contains("friend's unlock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task You_cannot_cheer_your_own_unlock()
    {
        await using var h = new ClaimTestHarness();
        var unlock = await h.Claim("SOLO_TRIP");

        var toggle = new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Cheer"), CancellationToken.None));

        Assert.Contains("your own", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task You_cannot_respect_your_own_unlock_either()
    {
        await using var h = new ClaimTestHarness();
        var unlock = await h.Claim("SOLO_TRIP");

        var toggle = new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock);

        await Assert.ThrowsAsync<DomainException>(() =>
            toggle.Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Respect"), CancellationToken.None));

        Assert.Empty(h.Db.Reactions);
    }

    [Fact]
    public async Task A_friend_can_still_react_to_my_unlock()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var unlock = await h.Claim("SOLO_TRIP");

        var result = await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Cheer"), CancellationToken.None);

        Assert.Equal(1, result.Cheers);
    }

    [Fact]
    public async Task Reaction_counts_come_back_on_the_feed()
    {
        await using var h = new ClaimTestHarness();
        var friend = h.AddUser("Nidha", "nidha@example.com");
        h.MakeFriends(h.UserId, friend);

        var unlock = await h.Claim("SOLO_TRIP");

        await new ToggleReactionCommandHandler(h.Db, new TestCurrentUser(friend), h.Clock)
            .Handle(new ToggleReactionCommand(unlock.UserAchievementId, "Respect"), CancellationToken.None);

        var item = (await Feed(h, h.UserId)).Single(f => f.AchievementName == "Solo Trip");

        Assert.Equal(1, item.Respects);
        Assert.False(item.IRespected);   // the friend respected it, not me
    }
}
