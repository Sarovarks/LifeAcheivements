using LifeAchievements.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LifeAchievements.Application.Abstractions;

/// <summary>
/// The slice of the DbContext the Application layer is allowed to see. Keeps handlers
/// testable (SQLite in-memory) without dragging Infrastructure into the Application layer.
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Achievement> Achievements { get; }
    DbSet<UserAchievement> UserAchievements { get; }
    DbSet<XpTransaction> XpTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
