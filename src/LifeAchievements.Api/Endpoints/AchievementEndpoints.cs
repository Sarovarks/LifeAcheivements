using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Application.Achievements.Queries;

namespace LifeAchievements.Api.Endpoints;

public static class AchievementEndpoints
{
    public static IEndpointRouteBuilder MapAchievementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/achievements")
            .RequireAuthorization()
            .WithTags("Achievements");

        group.MapGet("", (IDispatcher dispatcher, string? category, CancellationToken ct) =>
                dispatcher.Query(new GetAchievementsQuery(category), ct))
            .WithName("GetAchievements")
            .WithSummary("The whole catalogue with my status on each one. Hidden ones come back masked.");

        group.MapPost("/{id:guid}/claim", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
                dispatcher.Send(new ClaimAchievementCommand(id), ct))
            .WithName("ClaimAchievement")
            .WithSummary("Claim an achievement. Self-verified ones unlock immediately.");

        return app;
    }
}
