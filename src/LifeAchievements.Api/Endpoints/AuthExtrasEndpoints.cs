using LifeAchievements.Domain.Entities;
using LifeAchievements.Shared.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LifeAchievements.Api.Endpoints;

/// <summary>
/// Identity's built-in /auth/register only takes an email and a password. The game needs a
/// display name from the very first screen, so registration goes through /auth/signup, which
/// creates the account and then hands straight back to Identity's own login.
/// </summary>
public static class AuthExtrasEndpoints
{
    public static IEndpointRouteBuilder MapAuthExtras(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/signup", async (
            [FromBody] RegisterRequest request,
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore) =>
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return Results.Problem(title: "That did not work", detail: "Pick a display name.", statusCode: 400);

            var user = new AppUser
            {
                DisplayName = request.DisplayName.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                TotalXp = 0
            };

            await userStore.SetUserNameAsync(user, request.Email, CancellationToken.None);
            await ((IUserEmailStore<AppUser>)userStore).SetEmailAsync(user, request.Email, CancellationToken.None);

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Results.Problem(
                    title: "That did not work",
                    detail: string.Join(" ", result.Errors.Select(e => e.Description)),
                    statusCode: 400);
            }

            return Results.Ok(new { user.Id, user.DisplayName });
        })
        .AllowAnonymous()
        .WithName("Signup")
        .WithSummary("Create an account with a display name, then call /auth/login.");

        return app;
    }
}
