using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Application.Achievements.Queries;
using LifeAchievements.Domain.Exceptions;
using LifeAchievements.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

public class ProgressAndNotesTests
{
    private static LogProgressCommandHandler ProgressHandler(ClaimTestHarness h) =>
        new(h.Db, new TestCurrentUser(h.UserId), h.Clock);

    private static SetUnlockNoteCommandHandler NoteHandler(ClaimTestHarness h) =>
        new(h.Db, new TestCurrentUser(h.UserId));

    private static Task<LogProgressResultDto> Log(ClaimTestHarness h, string code, int amount = 1) =>
        ProgressHandler(h).Handle(new LogProgressCommand(h.AchievementId(code), amount), CancellationToken.None);

    private static async Task<AchievementDto> Fetch(ClaimTestHarness h, string code)
    {
        var handler = new GetAchievementsQueryHandler(h.Db, new TestCurrentUser(h.UserId), h.Clock);
        var all = await handler.Handle(new GetAchievementsQuery(null), CancellationToken.None);
        return all.Single(a => a.Code == code);
    }

    // ---------------- progress blocks the claim ----------------

    [Fact]
    public async Task A_multi_step_achievement_cannot_be_claimed_before_it_is_finished()
    {
        await using var h = new ClaimTestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("IRON_WILL"));

        Assert.Contains("not finished", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0 of 5 gym days", ex.Message);
        Assert.Equal(0, (await h.ReloadUser()).TotalXp);
    }

    [Fact]
    public async Task Partial_progress_still_blocks_the_claim()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL", 3);

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Claim("IRON_WILL"));
        Assert.Contains("3 of 5 gym days", ex.Message);
    }

    [Fact]
    public async Task Finishing_the_run_makes_it_claimable_but_does_not_auto_unlock()
    {
        await using var h = new ClaimTestHarness();

        var result = await Log(h, "IRON_WILL", 5);

        Assert.True(result.JustCompleted);
        Assert.True(result.Progress.IsComplete);

        // The trophy moment still belongs to the player - nothing unlocked itself.
        Assert.Equal(0, (await h.ReloadUser()).TotalXp);

        var dto = await Fetch(h, "IRON_WILL");
        Assert.Equal(AchievementStatuses.Claimable, dto.Status);
    }

    [Fact]
    public async Task Once_finished_the_claim_goes_through_normally()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL", 5);
        var unlock = await h.Claim("IRON_WILL");

        Assert.Equal(AchievementStatuses.Unlocked, unlock.Status);
        Assert.Equal(200, unlock.XpGained);
        await h.AssertLedgerMatchesCachedTotal();
    }

    [Fact]
    public async Task Claiming_cashes_in_the_run_and_starts_a_fresh_one()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL", 5);
        await h.Claim("IRON_WILL");

        var run = await h.Db.AchievementProgress.AsNoTracking()
            .SingleAsync(p => p.UserId == h.UserId && p.AchievementId == h.AchievementId("IRON_WILL"));

        Assert.Equal(0, run.Current);
    }

    // ---------------- logging ----------------

    [Fact]
    public async Task Progress_accumulates_across_calls()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL");
        await Log(h, "IRON_WILL");
        var third = await Log(h, "IRON_WILL");

        Assert.Equal(3, third.Progress.Current);
        Assert.Equal(5, third.Progress.Target);
        Assert.False(third.JustCompleted);
        Assert.Equal(60d, third.Progress.Percent, 3);
    }

    [Fact]
    public async Task Progress_never_goes_past_the_target()
    {
        await using var h = new ClaimTestHarness();

        var result = await Log(h, "LIBRARY_CARD", 100);

        Assert.Equal(12, result.Progress.Current);
        Assert.True(result.Progress.IsComplete);
    }

    [Fact]
    public async Task A_finished_run_refuses_more_logging()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL", 5);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Log(h, "IRON_WILL"));
        Assert.Contains("already finished", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_one_tap_achievement_cannot_have_progress_logged_against_it()
    {
        await using var h = new ClaimTestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(() => Log(h, "DEAR_DIARY"));
        Assert.Contains("just claim it", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_hidden_achievement_cannot_have_progress_logged_against_it()
    {
        await using var h = new ClaimTestHarness();

        await Assert.ThrowsAsync<DomainException>(() => Log(h, "NIGHT_OWL"));
    }

    [Fact]
    public async Task A_run_from_a_lapsed_week_does_not_count_towards_the_new_one()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL", 4);

        h.Clock.Advance(TimeSpan.FromDays(14));
        var result = await Log(h, "IRON_WILL");

        Assert.Equal(1, result.Progress.Current);
    }

    [Fact]
    public async Task A_run_with_no_window_keeps_accumulating_forever()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "TURF_LEGEND", 20);
        h.Clock.Advance(TimeSpan.FromDays(400));
        var result = await Log(h, "TURF_LEGEND", 20);

        Assert.Equal(40, result.Progress.Current);
    }

    // ---------------- the list query ----------------

    [Fact]
    public async Task An_unstarted_multi_step_achievement_reads_as_in_progress_at_zero()
    {
        await using var h = new ClaimTestHarness();

        var dto = await Fetch(h, "IRON_WILL");

        Assert.Equal(AchievementStatuses.InProgress, dto.Status);
        Assert.NotNull(dto.Progress);
        Assert.Equal(0, dto.Progress!.Current);
        Assert.Equal("gym days", dto.Progress.Unit);
        Assert.Equal("3 of 5 gym days this week", dto.Progress with { Current = 3 } is var p ? p.Summary : "");
    }

    [Fact]
    public async Task A_one_tap_achievement_carries_no_progress_block()
    {
        await using var h = new ClaimTestHarness();

        var dto = await Fetch(h, "DEAR_DIARY");

        Assert.Null(dto.Progress);
        Assert.Equal(AchievementStatuses.Claimable, dto.Status);
    }

    [Fact]
    public async Task The_list_reports_a_lapsed_run_as_zero_without_writing_to_the_database()
    {
        await using var h = new ClaimTestHarness();

        await Log(h, "IRON_WILL", 4);
        h.Clock.Advance(TimeSpan.FromDays(14));

        var dto = await Fetch(h, "IRON_WILL");
        Assert.Equal(0, dto.Progress!.Current);

        // The stale row is untouched until something actually writes to it.
        var row = await h.Db.AchievementProgress.AsNoTracking()
            .SingleAsync(p => p.AchievementId == h.AchievementId("IRON_WILL"));
        Assert.Equal(4, row.Current);
    }

    // ---------------- notes ----------------

    [Fact]
    public async Task A_note_can_be_attached_to_an_unlock()
    {
        await using var h = new ClaimTestHarness();

        var unlock = await h.Claim("SOLO_TRIP");
        await NoteHandler(h).Handle(
            new SetUnlockNoteCommand(unlock.UserAchievementId, "  Munnar. Got lost twice.  "),
            CancellationToken.None);

        var claim = await h.Db.UserAchievements.AsNoTracking()
            .SingleAsync(ua => ua.Id == unlock.UserAchievementId);

        Assert.Equal("Munnar. Got lost twice.", claim.Note);
    }

    [Fact]
    public async Task An_empty_note_clears_it_rather_than_storing_whitespace()
    {
        await using var h = new ClaimTestHarness();

        var unlock = await h.Claim("SOLO_TRIP");
        await NoteHandler(h).Handle(new SetUnlockNoteCommand(unlock.UserAchievementId, "something"), CancellationToken.None);
        await NoteHandler(h).Handle(new SetUnlockNoteCommand(unlock.UserAchievementId, "   "), CancellationToken.None);

        var claim = await h.Db.UserAchievements.AsNoTracking()
            .SingleAsync(ua => ua.Id == unlock.UserAchievementId);

        Assert.Null(claim.Note);
    }

    [Fact]
    public async Task A_note_cannot_be_attached_to_somebody_elses_unlock()
    {
        await using var h = new ClaimTestHarness();

        var unlock = await h.Claim("SOLO_TRIP");

        var stranger = new SetUnlockNoteCommandHandler(h.Db, new TestCurrentUser(Guid.NewGuid()));
        await Assert.ThrowsAsync<DomainException>(() =>
            stranger.Handle(new SetUnlockNoteCommand(unlock.UserAchievementId, "mine now"), CancellationToken.None));
    }

    [Fact]
    public async Task Recent_unlocks_carry_the_note()
    {
        await using var h = new ClaimTestHarness();

        var unlock = await h.Claim("SOLO_TRIP");
        await NoteHandler(h).Handle(
            new SetUnlockNoteCommand(unlock.UserAchievementId, "Munnar."), CancellationToken.None);

        var recent = await new GetRecentUnlocksQueryHandler(h.Db, new TestCurrentUser(h.UserId))
            .Handle(new GetRecentUnlocksQuery(10), CancellationToken.None);

        Assert.Equal("Munnar.", recent.Single(r => r.UserAchievementId == unlock.UserAchievementId).Note);
    }

    [Fact]
    public async Task The_claim_result_carries_the_id_the_note_endpoint_needs()
    {
        await using var h = new ClaimTestHarness();

        var unlock = await h.Claim("SOLO_TRIP");

        Assert.NotEqual(Guid.Empty, unlock.UserAchievementId);
        Assert.True(await h.Db.UserAchievements.AnyAsync(ua => ua.Id == unlock.UserAchievementId));
    }
}
