using LifeAchievements.Application.Abstractions;
using LifeAchievements.Application.Achievements.Commands;
using LifeAchievements.Application.Events;
using LifeAchievements.Domain.Entities;
using LifeAchievements.Domain.Enums;
using LifeAchievements.Domain.Events;
using LifeAchievements.Infrastructure.Persistence;
using LifeAchievements.Infrastructure.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LifeAchievements.Application.Tests;

/// <summary>
/// Runs the claim flow against a real (in-memory SQLite) database, so transactions,
/// relationships and the XP ledger behave the way they will in Postgres.
/// </summary>
public sealed class ClaimTestHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Db { get; }
    public TestClock Clock { get; }
    public Guid UserId { get; }

    /// <summary>The primary player's address, for tests that add friends the real way.</summary>
    public string UserEmail => "player@example.com";

    public ClaimTestHarness(DateTimeOffset? now = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();

        Clock = new TestClock(now ?? new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

        Db.Achievements.AddRange(AchievementCatalogue.All);

        UserId = Guid.NewGuid();
        Db.Users.Add(new AppUser
        {
            Id = UserId,
            DisplayName = "Test Player",
            Email = "player@example.com",
            NormalizedEmail = "PLAYER@EXAMPLE.COM",
            UserName = "player@example.com",
            NormalizedUserName = "PLAYER@EXAMPLE.COM",
            TotalXp = 0,
            CreatedAt = Clock.Now
        });

        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    /// <summary>The same approval pipeline the API wires up, with the hidden rules attached.</summary>
    public Achievements.IUnlockService Unlocks() =>
        new Achievements.UnlockService(Db, new TestEventDispatcher(new HiddenAchievementRulesHandler(Db)));

    public ClaimAchievementCommandHandler Handler() =>
        new(Db, new TestCurrentUser(UserId), Clock, Unlocks());

    public ClaimAchievementCommandHandler HandlerFor(Guid userId) =>
        new(Db, new TestCurrentUser(userId), Clock, Unlocks());

    /// <summary>Adds a second player, for the friends, feed and verification tests.</summary>
    public Guid AddUser(string displayName, string email, int totalXp = 0)
    {
        var id = Guid.NewGuid();
        Db.Users.Add(new AppUser
        {
            Id = id,
            DisplayName = displayName,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            TotalXp = totalXp,
            CreatedAt = Clock.Now
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return id;
    }

    /// <summary>Makes two people friends outright, skipping the request dance.</summary>
    public void MakeFriends(Guid a, Guid b)
    {
        Db.Friendships.Add(new Friendship
        {
            RequesterId = a,
            AddresseeId = b,
            Status = FriendshipStatus.Accepted,
            CreatedAt = Clock.Now,
            RespondedAt = Clock.Now
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public Task<Shared.Dtos.UnlockResultDto> ClaimAs(Guid userId, string code) =>
        HandlerFor(userId).Handle(new ClaimAchievementCommand(AchievementId(code)), CancellationToken.None);

    public Guid AchievementId(string code) => AchievementCatalogue.IdFor(code);

    /// <summary>Finishes a multi-step achievement's run so a claim can be tested past it.</summary>
    public async Task CompleteProgress(string code)
    {
        var achievement = await Db.Achievements.AsNoTracking().FirstAsync(a => a.Code == code);
        await new Achievements.Commands.LogProgressCommandHandler(Db, new TestCurrentUser(UserId), Clock)
            .Handle(new Achievements.Commands.LogProgressCommand(achievement.Id, achievement.TargetCount!.Value),
                CancellationToken.None);
    }

    public Task<Shared.Dtos.UnlockResultDto> Claim(string code) =>
        Handler().Handle(new ClaimAchievementCommand(AchievementId(code)), CancellationToken.None);

    public async Task<AppUser> ReloadUser()
    {
        Db.ChangeTracker.Clear();
        return await Db.Users.AsNoTracking().FirstAsync(u => u.Id == UserId);
    }

    /// <summary>The ledger is the truth: this must always equal the cached TotalXp.</summary>
    public async Task<int> LedgerTotal() =>
        await Db.XpTransactions.AsNoTracking().Where(x => x.UserId == UserId).SumAsync(x => x.Amount);

    public async Task AssertLedgerMatchesCachedTotal()
    {
        var user = await ReloadUser();
        Assert.Equal(await LedgerTotal(), user.TotalXp);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; set; } = now;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

public sealed class TestCurrentUser(Guid id) : ICurrentUser
{
    public Guid Id { get; } = id;
    public bool IsAuthenticated => true;
}

/// <summary>Stands in for the DI-backed dispatcher: calls the handlers it was handed.</summary>
public sealed class TestEventDispatcher(params object[] handlers) : IDomainEventDispatcher
{
    public async Task Dispatch<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent
    {
        foreach (var handler in handlers.OfType<IDomainEventHandler<TEvent>>())
            await handler.Handle(domainEvent, ct);
    }
}
