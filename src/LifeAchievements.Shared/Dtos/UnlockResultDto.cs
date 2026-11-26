namespace LifeAchievements.Shared.Dtos;

public record UnlockResultDto(
    Guid AchievementId,
    string Code,
    string Name,
    string Description,
    string Tier,
    string Icon,
    string Status,
    int XpGained,
    int NewTotalXp,
    int OldLevel,
    int NewLevel,
    string NewLevelTitle,
    bool LeveledUp,
    int XpToNextLevel,
    double ProgressPercent,
    IReadOnlyList<BonusUnlockDto> BonusUnlocks);

public record BonusUnlockDto(
    Guid AchievementId,
    string Code,
    string Name,
    string Description,
    string Tier,
    string Icon,
    int XpGained);
