namespace LifeAchievements.Shared.Dtos;

/// <summary>What the current user can do with an achievement right now.</summary>
public static class AchievementStatuses
{
    public const string Locked = "Locked";
    public const string Claimable = "Claimable";
    /// <summary>A multi-step achievement that has been started but not finished.</summary>
    public const string InProgress = "InProgress";
    public const string Pending = "Pending";
    public const string Unlocked = "Unlocked";
    public const string Cooldown = "Cooldown";
}
