using LifeAchievements.Application.Abstractions;
using LifeAchievements.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LifeAchievements.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<XpTransaction> XpTransactions => Set<XpTransaction>();
    public DbSet<AchievementProgress> AchievementProgress => Set<AchievementProgress>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Reaction> Reactions => Set<Reaction>();

    DbSet<AppUser> IAppDbContext.Users => Users;

    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        Database.BeginTransactionAsync(ct);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Npgsql refuses to write a DateTimeOffset whose offset is not zero into a
        // "timestamp with time zone" column. The game works in local time (a cooldown and a
        // 2 AM rule only make sense locally), so normalise to UTC on the way in. Comparisons
        // on DateTimeOffset are instant-based, so nothing about the rules changes.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(UtcConverter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(NullableUtcConverter);
            }
        }
    }

    private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcConverter =
        new(v => v.ToUniversalTime(), v => v);

    private static readonly ValueConverter<DateTimeOffset?, DateTimeOffset?> NullableUtcConverter =
        new(v => v.HasValue ? v.Value.ToUniversalTime() : v, v => v);
}
