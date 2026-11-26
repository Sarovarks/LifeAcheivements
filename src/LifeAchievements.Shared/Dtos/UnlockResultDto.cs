namespace LifeAchievements.Shared.Dtos;

public record UnlockResultDto(
    Guid UserAchievementId,
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

/// <summary>Attach a sentence about what actually happened, from the unlock screen.</summary>
public record SetUnlockNoteRequest(string? Note);

/// <summary>Log a step towards a multi-step achievement.</summary>
public record LogProgressRequest(int Amount = 1);

public record LogProgressResultDto(
    Guid AchievementId,
    string Name,
    ProgressDto Progress,
    bool JustCompleted);

public record BonusUnlockDto(
    Guid AchievementId,
    string Code,
    string Name,
    string Description,
    string Tier,
    string Icon,
    int XpGained);
