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

// Identity's built-in bearer-token endpoints: /auth/register, /auth/login, /auth/refresh, ...
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
