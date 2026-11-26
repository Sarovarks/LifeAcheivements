using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Queries;
using LifeAchievements.Application.Users.Queries;
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

        return app;
    }
}
