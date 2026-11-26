namespace LifeAchievements.Shared.Dtos;

public record MeDto(
    Guid Id,
    string DisplayName,
    string Email,
    int TotalXp,
    int Level,
    string LevelTitle,
    int XpToNextLevel,
    int NextLevelAtXp,
    double ProgressPercent,
    int UnlockedCount,
    int DayStreak,
    int FriendsRank);
