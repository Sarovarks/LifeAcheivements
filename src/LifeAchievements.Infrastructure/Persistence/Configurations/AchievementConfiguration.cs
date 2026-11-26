using LifeAchievements.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeAchievements.Infrastructure.Persistence.Configurations;

public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.ToTable("achievements");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();

        builder.Property(a => a.Name).HasMaxLength(128).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(512).IsRequired();
        builder.Property(a => a.Hint).HasMaxLength(256);
        builder.Property(a => a.Icon).HasMaxLength(64).IsRequired();

        builder.Property(a => a.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Tier).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.VerificationType).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.ProgressWindow).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.ProgressUnit).HasMaxLength(32);

        builder.Ignore(a => a.HasProgress);

        builder.HasIndex(a => a.Category);
    }
}
