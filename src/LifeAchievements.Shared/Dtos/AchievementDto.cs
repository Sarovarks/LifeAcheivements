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
    DateTimeOffset? ClaimableAgainAt,
    ProgressDto? Progress);

/// <summary>Where a multi-step achievement currently stands for this user.</summary>
public record ProgressDto(
    int Current,
    int Target,
    string Unit,
    string Window,
    string WindowLabel,
    double Percent,
    DateTimeOffset? ResetsAt)
{
    public bool IsComplete => Current >= Target;

    /// <summary>"3 of 5 gym days this week" - the line the card actually shows.</summary>
    public string Summary => $"{Current} of {Target} {Unit} {WindowLabel}";
}
