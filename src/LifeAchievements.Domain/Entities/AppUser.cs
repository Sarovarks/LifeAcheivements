using Microsoft.AspNetCore.Identity;

namespace LifeAchievements.Domain.Entities;

/// <summary>
/// The ASP.NET Core Identity user plus the game fields. One Users table, as per the brief.
/// TotalXp is a cached sum of the XpTransactions ledger - never write to it on its own.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public int TotalXp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
