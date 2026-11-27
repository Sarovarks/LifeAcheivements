# Testing traps

Three gaps that let a green suite sit over a broken system, with the guard tests that close
them. Each was found in production, not in review.

## Contents

- [Unregistered handlers](#unregistered-handlers)
- [SQLite is not PostgreSQL](#sqlite-is-not-postgresql)
- [Seeded state skips the real path](#seeded-state-skips-the-real-path)
- [Verifying a guard](#verifying-a-guard)
- [What tests cannot catch](#what-tests-cannot-catch)

## Unregistered handlers

With a hand-written dispatcher, handlers are registered one line at a time. Tests construct
handlers directly, so nothing in the suite asks the container for one — and a handler you
forgot to register passes every test, then throws `No handler registered for X` on the first
real request.

Reflect over the assembly instead:

```csharp
[Fact]
public void Every_command_and_query_has_a_registered_handler()
{
    var services = new ServiceCollection();
    services.AddApplication();
    var registered = services.Select(d => d.ServiceType).ToHashSet();

    var missing = new List<string>();

    foreach (var type in typeof(ICommand<>).Assembly.GetTypes()
                 .Where(t => t is { IsAbstract: false, IsInterface: false }))
    {
        foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
        {
            var definition = contract.GetGenericTypeDefinition();
            var handler =
                definition == typeof(ICommand<>) ? typeof(ICommandHandler<,>) :
                definition == typeof(IQuery<>) ? typeof(IQueryHandler<,>) : null;

            if (handler is null) continue;

            var expected = handler.MakeGenericType(type, contract.GetGenericArguments()[0]);
            if (!registered.Contains(expected))
                missing.Add($"{type.Name} -> no handler registered");
        }
    }

    Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
}
```

Inspecting the `ServiceCollection` rather than building a provider means no real dependencies
are needed — you are checking registration, not resolution, which is the thing that was missing.

Pair it with a test asserting the reflection matches at least one command and one query. A
reflection bug that matches nothing would otherwise pass silently and prove only that the loop
ran.

## SQLite is not PostgreSQL

An in-memory SQLite database is an excellent test target — real SQL, real constraints, no
mocks. But the differences bite in specific places:

| Behaviour | SQLite | PostgreSQL |
|---|---|---|
| `ORDER BY`/compare a `DateTimeOffset` | throws `NotSupportedException` | fine |
| Timestamp precision | stores exactly | microseconds |
| Non-zero UTC offset | accepts | Npgsql refuses outright |

Consequences worth internalising:

**Equality on a timestamp passes in tests and matches nothing live.** Never find rows by
"happened at the same moment"; return ids from the operation that created them.

**Ordering by a `DateTimeOffset` fails in tests but would work in production** — the opposite
direction. Sort in memory when the set is bounded (one user and their friends), and leave a
comment saying why, so the next person does not "optimise" it into a crash. For anything
unbounded, store a UTC `DateTime` and sort in the database.

**Timezone rules need `ToLocalTime()`.** With a global UTC converter in place, a rule about
"before 6 AM" or "the same calendar day" is evaluated in UTC unless you convert back. For a
user in UTC+5:30 that is a five-and-a-half-hour error — small enough to look like a flaky
edge case rather than a bug.

A related rule worth testing directly: when two rules talk about the same thing — a cooldown
and a streak both reasoning about "a day" — assert that they agree. They are written at
different times and drift apart silently.

## Seeded state skips the real path

A test helper that inserts an already-accepted relationship row is convenient, and every social
test will quietly use it. The result is that the actual sequence — send a request, accept it,
then read — is never executed once, which is precisely where a bug would hide.

Keep the helper for setting up unrelated state, but have at least one test per flow drive the
real commands end to end:

```csharp
var id = await new SendRequestHandler(...).Handle(new SendRequest(email), ct);
await new RespondHandler(...).Handle(new Respond(id, Accept: true), ct);
// now assert what the other person can actually see
```

The general form: **if a helper constructs a state that the application normally reaches
through a sequence of operations, at least one test must take the sequence.**

## Verifying a guard

A guard test must be broken on purpose before it is trusted. Remove the registration, delete
the constraint, revert the fix — confirm it goes red, then restore it.

A test that cannot fail is worse than no test, because it manufactures confidence that stops
anyone looking. This takes thirty seconds and is the difference between a guard and decoration.

## What tests cannot catch

Worth being honest about the boundary. A suite running against an in-memory database and
directly constructed handlers cannot see:

- **Dependency injection wiring** — unless you assert it, as above
- **Anything above the application layer** — routing, model binding, auth, serialisation
- **Database-specific translation** — until it runs against the real engine
- **Whether a deploy happened** — a green build says nothing about what is live
- **How anything looks** — rendering it and looking is the only check that works

These are the gaps that a live smoke test covers (`scripts/smoke_test.py`), which is why the
main skill treats "reproduce against production" as a habit rather than a last resort. Tests
make a fix safe to make; they do not tell you the system works.
