using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Queries;
using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Application.Users.Queries;
using LifeAchievements.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace LifeAchievements.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me")
            .RequireAuthorization()
            .WithTags("Me");

        group.MapGet("", (IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Query(new GetMeQuery(), ct))
            .WithName("GetMe")
            .WithSummary("Level, XP, streak and headline stats for the signed-in player.");

        group.MapGet("/unlocks", (IDispatcher dispatcher, int take, CancellationToken ct) =>
                dispatcher.Query(new GetRecentUnlocksQuery(take <= 0 ? 10 : take), ct))
            .WithName("GetRecentUnlocks")
            .WithSummary("Most recent approved unlocks, newest first.");

        group.MapPut("/unlocks/{id:guid}/note", async (Guid id, [FromBody] SetUnlockNoteRequest request,
                IDispatcher dispatcher, CancellationToken ct) =>
            {
                await dispatcher.Send(new SetUnlockNoteCommand(id, request.Note), ct);
                return Results.NoContent();
            })
            .WithName("SetUnlockNote")
            .WithSummary("Attach a line about what actually happened, from the unlock screen.");

        return app;
    }
}
