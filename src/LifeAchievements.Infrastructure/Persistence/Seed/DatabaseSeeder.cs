using LifeAchievements.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeAchievements.Infrastructure.Persistence.Seed;

/// <summary>
/// Applies migrations and syncs the achievement catalogue. Keyed on Code, so running it on
/// every startup is safe: new badges are added, existing ones are refreshed, nothing is lost.
/// </summary>
public class DatabaseSeeder(AppDbContext db, ILogger<DatabaseSeeder> logger)
{
    public async Task MigrateAndSeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedAchievementsAsync(ct);
    }

    public async Task SeedAchievementsAsync(CancellationToken ct = default)
    {
        var existing = await db.Achievements.ToDictionaryAsync(a => a.Code, ct);
        var added = 0;
        var updated = 0;

        foreach (var seed in AchievementCatalogue.All)
        {
            if (!existing.TryGetValue(seed.Code, out var current))
            {
                db.Achievements.Add(seed);
                added++;
                continue;
            }

            // Copy is deliberate: the catalogue is the source of truth for copy and balance,
            // but the row keeps its identity so nobody loses an unlock when the text changes.
            if (current.Name == seed.Name && current.Description == seed.Description
                && current.Category == seed.Category && current.Tier == seed.Tier
                && current.XpReward == seed.XpReward && current.IsHidden == seed.IsHidden
                && current.Hint == seed.Hint && current.IsRepeatable == seed.IsRepeatable
                && current.CooldownHours == seed.CooldownHours
                && current.VerificationType == seed.VerificationType && current.Icon == seed.Icon
                && current.TargetCount == seed.TargetCount && current.ProgressUnit == seed.ProgressUnit
                && current.ProgressWindow == seed.ProgressWindow)
            {
                continue;
            }

            current.Name = seed.Name;
            current.Description = seed.Description;
            current.Category = seed.Category;
            current.Tier = seed.Tier;
            current.XpReward = seed.XpReward;
            current.IsHidden = seed.IsHidden;
            current.Hint = seed.Hint;
            current.IsRepeatable = seed.IsRepeatable;
            current.CooldownHours = seed.CooldownHours;
            current.VerificationType = seed.VerificationType;
            current.Icon = seed.Icon;
            current.TargetCount = seed.TargetCount;
            current.ProgressUnit = seed.ProgressUnit;
            current.ProgressWindow = seed.ProgressWindow;
            updated++;
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Achievement catalogue seeded: {Added} added, {Updated} updated.", added, updated);
        }
    }
}
