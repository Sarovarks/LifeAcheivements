using LifeAchievements.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Friends.Commands;

public record MarkNotificationsSeenCommand : ICommand<bool>;

/// <summary>
/// Opening the bell clears the badge. The timestamp moves to now, so anything that arrives
/// afterwards counts as new again - the list itself never changes, only what is marked.
/// </summary>
public class MarkNotificationsSeenCommandHandler(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<MarkNotificationsSeenCommand, bool>
{
    public async Task<bool> Handle(MarkNotificationsSeenCommand command, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.Id, ct);
        if (user is null) return false;

        user.LastSeenNotificationsAt = clock.Now;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
