namespace LifeAchievements.Shared.Dtos;

public record RecentUnlockDto(
    Guid UserAchievementId,
    Guid AchievementId,
    string Name,
    string Icon,
    string Tier,
    int XpGained,
    DateTimeOffset UnlockedAt,
    string? Note);
