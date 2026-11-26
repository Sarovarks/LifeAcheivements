using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Infrastructure.Persistence;
using LifeAchievements.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeAchievements.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Kept out of appsettings.json on purpose - locally it lives in user-secrets, and in a
        // real environment it comes from the host (env var ConnectionStrings__Default).
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "No database connection string. Set it with:\n" +
                "  dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\" " +
                "--project src/LifeAchievements.Api\n" +
                "See the README for the value that matches docker-compose.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseSeeder>();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddApiEndpoints();

        return services;
    }
}
