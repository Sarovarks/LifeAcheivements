using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Friends.Commands;
using LifeAchievements.Application.Friends.Queries;
using LifeAchievements.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace LifeAchievements.Api.Endpoints;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder app)
    {
        var friends = app.MapGroup("/friends").RequireAuthorization().WithTags("Friends");

        friends.MapGet("", (IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Query(new GetFriendsQuery(), ct))
            .WithName("GetFriends")
            .WithSummary("Accepted friends, plus any requests waiting on either side.");

        friends.MapPost("/requests", async ([FromBody] SendFriendRequestRequest request,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var id = await dispatcher.Send(new SendFriendRequestCommand(request.Email), ct);
                return Results.Ok(new { friendshipId = id });
            })
            .WithName("SendFriendRequest")
            .WithSummary("Add a friend by their exact email address.");

        friends.MapPost("/requests/{id:guid}", async (Guid id,
                [FromBody] RespondToFriendRequestRequest request,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                var accepted = await dispatcher.Send(new RespondToFriendRequestCommand(id, request.Accept), ct);
                return Results.Ok(new { accepted });
            })
            .WithName("RespondToFriendRequest")
            .WithSummary("Accept or decline a request somebody sent you.");

        friends.MapDelete("/{userId:guid}", async (Guid userId, IDispatcher dispatcher, CancellationToken ct) =>
            {
                await dispatcher.Send(new RemoveFriendCommand(userId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveFriend")
            .WithSummary("Remove a friend.");

        friends.MapGet("/leaderboard", (IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Query(new GetWeeklyLeaderboardQuery(), ct))
            .WithName("GetWeeklyLeaderboard")
            .WithSummary("XP earned since Monday, you and your friends.");

        // ---------------------------------------------------------------- feed --

        var feed = app.MapGroup("/feed").RequireAuthorization().WithTags("Feed");

        // take is optional: a non-nullable int here makes it a REQUIRED query parameter, so
        // GET /feed answers 400 rather than returning the feed. The app always sends one, so
        // nothing broke - but the obvious URL being an error is a trap for anything else that
        // calls this, and the handler already copes with a missing value.
        feed.MapGet("", (IDispatcher dispatcher, int? take, CancellationToken ct) =>
                dispatcher.Query(new GetFeedQuery(take is > 0 ? take.Value : 30), ct))
            .WithName("GetFeed")
            .WithSummary("Recent unlocks from you and your friends, with notes and reactions.");

        feed.MapPost("/{id:guid}/reactions", ([FromBody] ToggleReactionRequest request, Guid id,
                IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Send(new ToggleReactionCommand(id, request.Type), ct))
            .WithName("ToggleReaction")
            .WithSummary("Cheer or Respect an unlock. Sending the same one again takes it back.");

        // -------------------------------------------------------- verification --

        var verifications = app.MapGroup("/verifications").RequireAuthorization().WithTags("Verification");

        verifications.MapGet("", (IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Query(new GetPendingVerificationsQuery(), ct))
            .WithName("GetPendingVerifications")
            .WithSummary("Friends' claims waiting for somebody to vouch for them.");

        verifications.MapPost("/{id:guid}", ([FromBody] VerifyClaimRequest request, Guid id,
                IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Send(new VerifyClaimCommand(id, request.Approve), ct))
            .WithName("VerifyClaim")
            .WithSummary("Vouch for a friend's claim, or turn it down.");

        return app;
    }
}
