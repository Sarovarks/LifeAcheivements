using FluentValidation;
using LifeAchievements.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LifeAchievements.Api.Infrastructure;

/// <summary>
/// Turns broken game rules into a friendly 400 ProblemDetails instead of a 500.
/// The Detail text is written to be shown straight to the player.
/// </summary>
public sealed class GameExceptionHandler(IProblemDetailsService problemDetails, ILogger<GameExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case DomainException domain:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "That did not work",
                    Detail = domain.Message,
                    Type = "https://lifeachievements.app/errors/rule-broken"
                };
                break;

            case ValidationException validation:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request",
                    Detail = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)),
                    Type = "https://lifeachievements.app/errors/validation"
                };
                break;

            default:
                logger.LogError(exception, "Unhandled exception on {Path}", context.Request.Path);
                return false;
        }

        context.Response.StatusCode = problem.Status!.Value;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
