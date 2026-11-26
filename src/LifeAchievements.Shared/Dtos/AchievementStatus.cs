namespace LifeAchievements.Shared.Dtos;

/// <summary>What the current user can do with an achievement right now.</summary>
public static class AchievementStatuses
{
    public const string Locked = "Locked";
    public const string Claimable = "Claimable";
    public const string Pending = "Pending";
    public const string Unlocked = "Unlocked";
    public const string Cooldown = "Cooldown";
}
