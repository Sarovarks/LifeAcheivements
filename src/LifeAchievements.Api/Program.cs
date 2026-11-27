using LifeAchievements.Api.Endpoints;
using LifeAchievements.Api.Infrastructure;
using LifeAchievements.Application;
using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Infrastructure;
using LifeAchievements.Infrastructure.Persistence;
using LifeAchievements.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Render, Railway, Fly and friends hand the container a port in PORT and expect the app to
// listen on it. ASP.NET Core does not read that variable on its own, so without this the
// platform health check never connects and the deploy is marked failed.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GameExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);
builder.Services.AddAuthorization();

// The Blazor WASM app runs on its own origin, so it needs CORS to talk to the API.
const string BlazorCors = "blazor";
builder.Services.AddCors(options => options.AddPolicy(BlazorCors, policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                       ?? ["https://localhost:7200", "http://localhost:5200"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(BlazorCors);
app.UseAuthentication();
app.UseAuthorization();

// Liveness: is the process up at all. Deliberately does not touch the database, so a
// database blip does not get the container restarted out from under a working app.
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithTags("Health");

// Readiness: can it actually serve traffic, database included.
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Identity also maps /auth/register, which only takes an email and a password - so an
// account created through it has no display name and shows up nameless in the feed. The game
// asks for a name on the first screen, so /auth/signup is the only way in. MapIdentityApi
// cannot leave one endpoint out, so it is turned away here instead.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Equals("/auth/register", StringComparison.OrdinalIgnoreCase))
    {
        await Results.Problem(
            title: "Use /auth/signup",
            detail: "Registration needs a display name. Post email, password and displayName to /auth/signup.",
            statusCode: StatusCodes.Status404NotFound).ExecuteAsync(context);
        return;
    }

    await next();
});

// Identity's built-in bearer-token endpoints: /auth/login, /auth/refresh, ...
app.MapGroup("/auth").MapIdentityApi<AppUser>().WithTags("Auth");

app.MapAuthExtras();
app.MapMeEndpoints();
app.MapAchievementEndpoints();
app.MapFriendEndpoints();

// Migrate and sync the achievement catalogue on startup so a fresh clone just works.
// Set RunMigrationsOnStartup=false where migrations are applied as a deployment step instead -
// several replicas starting at once should not all be migrating the same database.
if (builder.Configuration.GetValue("RunMigrationsOnStartup", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.MigrateAndSeedAsync();
}

app.Run();

public partial class Program;
