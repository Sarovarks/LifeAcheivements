using LifeAchievements.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeAchievements.Infrastructure.Persistence.Configurations;

public class AchievementProgressConfiguration : IEntityTypeConfiguration<AchievementProgress>
{
    public void Configure(EntityTypeBuilder<AchievementProgress> builder)
    {
        builder.ToTable("achievement_progress");
        builder.HasKey(p => p.Id);

        builder.HasOne(p => p.Achievement)
            .WithMany()
            .HasForeignKey(p => p.AchievementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One run per user per achievement.
        builder.HasIndex(p => new { p.UserId, p.AchievementId }).IsUnique();
    }
}
