namespace LifeAchievements.Shared.Dtos;

public record AchievementDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Category,
    string Tier,
    int XpReward,
    bool IsHidden,
    string? Hint,
    bool IsRepeatable,
    int? CooldownHours,
    string VerificationType,
    string Icon,
    string Status,
    int TimesUnlocked,
    DateTimeOffset? LastUnlockedAt,
    DateTimeOffset? ClaimableAgainAt);
