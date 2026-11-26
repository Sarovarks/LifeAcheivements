using LifeAchievements.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeAchievements.Infrastructure.Persistence.Configurations;

public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("user_achievements");
        builder.HasKey(ua => ua.Id);

        builder.Property(ua => ua.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(ua => ua.Achievement)
            .WithMany(a => a.UserAchievements)
            .HasForeignKey(ua => ua.AchievementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(ua => ua.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ua => new { ua.UserId, ua.AchievementId });
        builder.HasIndex(ua => new { ua.UserId, ua.Status, ua.ApprovedAt });
    }
}
