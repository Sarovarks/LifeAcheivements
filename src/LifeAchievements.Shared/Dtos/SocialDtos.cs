namespace LifeAchievements.Shared.Dtos;

// ---------------------------------------------------------------- friends --

public record FriendDto(
    Guid UserId,
    string DisplayName,
    int Level,
    string LevelTitle,
    int TotalXp,
    int DayStreak,
    DateTimeOffset FriendsSince);

/// <summary>A request waiting on somebody. Direction says whose move it is.</summary>
public record FriendRequestDto(
    Guid FriendshipId,
    Guid UserId,
    string DisplayName,
    int Level,
    string LevelTitle,
    string Direction,
    DateTimeOffset SentAt);

public static class RequestDirections
{
    /// <summary>They asked you - you can accept or decline.</summary>
    public const string Incoming = "Incoming";

    /// <summary>You asked them - you are waiting.</summary>
    public const string Outgoing = "Outgoing";
}

public record FriendsOverviewDto(
    IReadOnlyList<FriendDto> Friends,
    IReadOnlyList<FriendRequestDto> Requests);

public record SendFriendRequestRequest(string Email);

public record RespondToFriendRequestRequest(bool Accept);

// ------------------------------------------------------------------- feed --

public record FeedItemDto(
    Guid UserAchievementId,
    Guid UserId,
    string DisplayName,
    int Level,
    Guid AchievementId,
    string AchievementName,
    string Description,
    string Icon,
    string Tier,
    int XpGained,
    string? Note,
    DateTimeOffset UnlockedAt,
    int Cheers,
    int Respects,
    bool ICheered,
    bool IRespected,
    bool IsMine);

public record ToggleReactionRequest(string Type);

public record ReactionResultDto(
    Guid UserAchievementId,
    int Cheers,
    int Respects,
    bool ICheered,
    bool IRespected);

// ----------------------------------------------------------- verification --

/// <summary>A friend's claim waiting for somebody to vouch for it.</summary>
public record PendingVerificationDto(
    Guid UserAchievementId,
    Guid UserId,
    string DisplayName,
    int Level,
    Guid AchievementId,
    string AchievementName,
    string Description,
    string Icon,
    string Tier,
    int XpReward,
    string? Note,
    DateTimeOffset ClaimedAt);

public record VerifyClaimRequest(bool Approve);

public record VerifyClaimResultDto(
    Guid UserAchievementId,
    string DisplayName,
    string AchievementName,
    bool Approved,
    int XpAwarded);

// ------------------------------------------------------------ leaderboard --

public record LeaderboardEntryDto(
    int Rank,
    Guid UserId,
    string DisplayName,
    int Level,
    string LevelTitle,
    int XpThisWeek,
    int UnlocksThisWeek,
    bool IsMe);

public record WeeklyLeaderboardDto(
    DateTimeOffset WeekStartedAt,
    DateTimeOffset WeekEndsAt,
    IReadOnlyList<LeaderboardEntryDto> Entries)
{
    /// <summary>Top three, for the podium.</summary>
    public IReadOnlyList<LeaderboardEntryDto> Podium => Entries.Take(3).ToList();

    public LeaderboardEntryDto? Me => Entries.FirstOrDefault(e => e.IsMe);
}

/// <summary>
/// One thing that happened to you while you were not looking. Kind is "Reaction",
/// "FriendRequest" or "Verification", so the UI can pick an icon without parsing text.
/// </summary>
public record NotificationDto(
    string Kind,
    string Title,
    string Detail,
    DateTimeOffset At,
    bool IsNew,
    Guid? UserAchievementId,
    Guid? FriendshipId);

public record NotificationsDto(int NewCount, IReadOnlyList<NotificationDto> Items);
