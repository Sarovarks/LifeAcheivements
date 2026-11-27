# Scaffolding

Solution layout, the dispatcher, and the EF Core conventions that avoid the traps.

## Contents

- [Solution layout](#solution-layout)
- [Package versions](#package-versions)
- [The dispatcher](#the-dispatcher)
- [EF Core conventions](#ef-core-conventions)
- [Patterns worth copying](#patterns-worth-copying)

## Solution layout

```
App.sln
├── docker-compose.yml                → Postgres for local development
├── src/
│   ├── App.Domain                    → entities, enums, domain events, rules
│   ├── App.Application               → commands, queries, handlers, validators
│   ├── App.Infrastructure            → DbContext, EF configs, migrations, seeding
│   ├── App.Api                       → endpoints, auth, DI, middleware, CORS
│   ├── App.Web                       → Blazor WASM PWA
│   └── App.Shared                    → DTOs shared by Api and Web
└── tests/
    ├── App.Domain.Tests
    └── App.Application.Tests
```

Dependencies point inward: `Api → Infrastructure → Application → Domain`. `Shared` is
referenced by `Api` and `Web` only — it exists so the HTTP contract is one set of types rather
than two that drift.

Domain and Application have no EF Core dependency beyond an `IAppDbContext` abstraction, which
is what keeps their tests fast and free of infrastructure.

## Package versions

Pin everything to the target framework's major version. Package managers happily resolve a
newer major that does not support your TFM, and the error it produces points at the wrong
thing. If `dotnet restore` complains about compatibility, check versions before anything else.

Put versions in `Directory.Packages.props` if the solution has more than a few projects — one
place to bump, no drift between csproj files.

## The dispatcher

CQRS here needs about forty lines, not a dependency with a licence to track.

```csharp
public interface ICommand<TResponse>;
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken ct);
}
```

The dispatcher resolves the handler from DI, runs any FluentValidation validators registered
for the message, and invokes it. Validation belongs here rather than in each handler, so every
message is validated the same way and no handler can forget.

**The cost of hand-rolling: registration is manual.** Nothing scans the assembly, so a handler
you forget to register compiles, passes every test that constructs it directly, and throws on
the first real request. The guard test in `testing-traps.md` closes exactly this gap — add it
when you add the dispatcher, not after it bites.

## EF Core conventions

**Npgsql rejects a non-zero UTC offset.** `DateTimeOffset.Now` carries your local offset and
`timestamptz` will not take it. Convert globally:

```csharp
private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcConverter =
    new(v => v.ToUniversalTime(), v => v);
```

Apply it to every `DateTimeOffset` property in `OnModelCreating`. Then remember the other half:
any rule that means *local* time — "before 6 AM", "same calendar day" — must call
`ToLocalTime()` first, or "2 AM" silently means 2 AM UTC.

**Never match rows on a timestamp.** Postgres keeps microseconds that SQLite does not, so
`WHERE CreatedAt == now` passes in tests and matches nothing in production. Carry ids instead:
if a handler needs to know which rows an operation produced, return their ids from the
operation rather than re-finding them by time.

**Seed idempotently, keyed on a natural code.** Derive each row's id deterministically from its
code (an MD5-derived GUID works well), so every environment agrees and re-seeding updates copy
without destroying user data. Run it on startup behind a config flag:

```csharp
if (builder.Configuration.GetValue("RunMigrationsOnStartup", defaultValue: true))
{
    await seeder.MigrateAndSeedAsync();
}
```

Migrating on startup means a fresh clone just works, and a deploy applies pending migrations
with no extra step. The tradeoff is a single replica — scaling out means moving migrations into
the deploy step first. Note it rather than discovering it.

**Think about delete behaviour before you need it.** Two foreign keys from one table to the
same parent cannot both cascade; one must be `Restrict`, which means deleting that parent fails
until those rows are removed explicitly. That is correct, but only if it is written down —
comment the constraint where it is configured and again in any cleanup script.

## Patterns worth copying

**Ledger plus cached total.** Where a number accumulates (points, balance, score), store every
change as a row and keep the total as a cached sum written in the same transaction. Never
update the total alone. Then assert in tests that the ledger equals the cached total after
every scenario — it catches a whole class of drift, and it makes date-ranged questions
("this week") trivial because they are just a filter over the ledger.

**Compute what can be computed.** Levels from a threshold table, streaks from the dates of
existing rows. Not stored means no migration to rebalance and no backfill to get wrong.

**One pipeline per outcome, not per caller.** When two paths produce the same result — a
self-service action and an approval flow, say — extract the shared pipeline *before* writing
the second one. Writing it twice is how one of them quietly skips a side effect.

**Domain events for side effects.** Raise an event when something meaningful happens and let
handlers react. Keeps the primary operation readable and makes the side effects individually
testable.
