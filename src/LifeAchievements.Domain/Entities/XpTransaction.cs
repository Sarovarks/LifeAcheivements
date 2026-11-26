namespace LifeAchievements.Domain.Entities;

/// <summary>
/// The XP ledger. Every single XP change is a row here; AppUser.TotalXp is only a cached sum
/// kept in step inside the same database transaction.
/// </summary>
public class XpTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? SourceUserAchievementId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
