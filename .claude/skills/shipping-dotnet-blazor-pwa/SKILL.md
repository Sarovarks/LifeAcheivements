---
name: shipping-dotnet-blazor-pwa
description: >-
  The stack, project layout and deployment setup for building a .NET web app with a Blazor
  WebAssembly PWA front end and shipping it live on free hosting (Cloudflare Pages + Render +
  Neon Postgres), plus the verification habits that stop silent breakage. Use this whenever
  starting a new .NET or ASP.NET Core web app, a Blazor WASM or PWA front end, a Clean
  Architecture / CQRS backend, or an EF Core + PostgreSQL project — and whenever the task is
  deploying, hosting or publishing one for free, wiring GitHub Actions for it, debugging a
  service worker or stale-cache problem, or working out why a deploy did not take effect. It
  applies even when the user only says "build me an app", "set up a new project", "host this
  somewhere free", "the site is showing an old version", or "I pushed but nothing changed".
---

# Shipping a .NET + Blazor WASM PWA

This is a working stack and the setup around it: how the solution is laid out, what deploys
where, and — the part that is easy to skip and expensive to skip — how to confirm that what you
built is actually the thing now running.

It carries no opinions about what the app *does*. Swap the domain, keep the plumbing.

## Why this exists

Most of the cost in a small web app is not the features. It is the hours lost to a deploy that
silently did not happen, a browser serving a four-build-old bundle, a test suite that is green
over an endpoint that cannot start, and an afternoon of theorising about a bug that a single
live request would have identified in a minute.

Each section below encodes one of those. If you are short on time, the two that repay
attention fastest are **Verify, don't assume** and **Blazor PWA traps**.

## The stack

| Layer | Choice | Why this one |
|---|---|---|
| Backend | ASP.NET Core minimal APIs, latest LTS .NET | Small surface, fast start, no controller ceremony |
| Architecture | Clean Architecture + CQRS | `Api → Infrastructure → Application → Domain`, dependencies inward |
| Dispatcher | Hand-written, ~40 lines | Avoids the MediatR licence question entirely |
| Data | EF Core + Npgsql, PostgreSQL | Free managed tier exists; EF migrations are enough at this size |
| Front end | Blazor WebAssembly, PWA | One language across the stack; installable on a phone |
| Styling | Plain CSS with custom properties | A component library fights any custom look |
| Tests | xUnit, SQLite in-memory for the data layer | Fast, real SQL, no mocks of the database |
| Local infra | Docker Compose (Postgres only) | One `docker compose up` to a working dev environment |

Start from `references/scaffolding.md` for the solution layout, the dispatcher, and the EF
conventions that avoid the common Npgsql and seeding traps.

## The hosting shape

Three free tiers, each doing the one thing it is best at:

```
Browser ──> Cloudflare Pages (static Blazor output, root domain, cache headers)
                 │
                 └── HTTPS ──> Render (API container, 750 instance-hours/month)
                                    │
                                    └──> Neon (Postgres, scales to zero)
```

No payment method on any of them. That is a stronger guarantee than any plan description:
without a card they suspend at the limit rather than bill.

The one real cost is a cold start — a free Render instance sleeps after 15 idle minutes and
takes roughly a minute to wake. Say so up front; it surprises people otherwise.

`references/hosting.md` has the full setup in order, including the GitHub App installation
step that is easy to miss.

## Build order

Working in this order means each layer is exercised by the time the next one needs it.

1. **Solution and Docker Compose** — projects, package versions pinned, Postgres running
   locally. Confirm `dotnet build` and `docker compose up` before writing features.
2. **Domain, then Application** — entities and rules first, with tests; then commands,
   queries and validators. Both layers are pure and fast to test.
3. **Infrastructure** — DbContext, configurations, first migration, seed data.
4. **Api** — endpoints, auth, CORS, ProblemDetails, health endpoints.
5. **Web** — Blazor pages against the real API.
6. **Deploy** — containers, GitHub Actions, the three hosts.

Deploy early even if the app is half-built. A deployment pipeline discovered on day one is a
task; the same pipeline discovered on day five is an outage.

## Verify, don't assume

This is the section that earns its keep. Each rule below exists because its absence cost real
hours.

**A deploy is not done when the push succeeds.** It is done when the live system reports the
commit you pushed. Give the API a version endpoint on day one:

```csharp
app.MapGet("/health/version", () => Results.Ok(new
    {
        commit = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") ?? "local",
        branch = Environment.GetEnvironmentVariable("RENDER_GIT_BRANCH") ?? "local",
        startedAt = StartedAt
    }))
    .AllowAnonymous();
```

Without it, the only way to tell which build is live is to exercise a behaviour that changed in
it — which means creating a test account and reproducing a flow. With it, one request answers.
Compare it to `git rev-parse HEAD` after every deploy.

**Split liveness from readiness.** `/health/live` must not touch the database. If it does, a
database blip gets the container killed and restarted in a loop instead of merely taken out of
rotation. `/health/ready` checks the database.

**Reproduce against production before theorising.** When a user reports something, write a
script that drives the live API the way a stranger would: register, act, read back. It settles
in a minute what an hour of reading code will not, and it routinely finds a second bug nobody
reported. `scripts/smoke_test.py` is a template to adapt — it is deliberately verbose about
which step failed.

Ask before creating accounts on a live system, and prefer addresses that are obviously
disposable so cleanup is a one-line `WHERE`.

**A status code is not a verification.** A 200 from a renamed site can be last week's content.
Check the thing that proves it: the asset hash, the `<base href>`, the config the page will
actually use.

**When a deploy did not happen, trigger one by hand first.** Every managed host has a "deploy
latest commit" button. It gets you current in minutes and separates two questions that look
identical from outside: whether the build works, and whether the host heard about your push.
Fix the trigger afterwards — a manual deploy does not repair a broken connection, and the next
push will fail just as silently.

## Blazor PWA traps

These are specific, non-obvious, and each one looks fine until it very much does not.
`references/blazor-pwa.md` covers them in full; the headlines:

**The published service worker is cache-first, and that beats every cache header you set.** A
service worker answers before the network is consulted, so `no-store` on your CDN never reaches
a returning visitor. Use the network-first worker in `assets/service-worker.published.js` — it
also caches assets individually (one bad asset no longer fails the whole install), and calls
`skipWaiting` and `clients.claim` so a new build takes over on the next load instead of
whenever every tab happens to close.

**Blazor WASM runs in the browser, so anything baked in at publish time is fixed for the life
of that build.** Write the API address into `wwwroot/appsettings.json` at deploy time, not
compile time. One artifact then runs against local, staging or production.

**Rewriting files after publish invalidates the service worker's integrity hashes.** If your
pipeline edits `appsettings.json` or `index.html` after `dotnet publish`, re-stamp the hashes
with `scripts/fix_service_worker_integrity.py`, or the app quietly stops being installable while
appearing to work.

**Unfingerprinted files need explicit cache rules.** `_framework/*` is content-hashed and can be
cached forever; `css/app.css` and anything you hand-write under `js/` keeps its name across
builds and must be `no-cache`, or a style fix looks like it never deployed. See
`assets/_headers`.

**Serve the app from a root domain if you can.** A project subpath (`user.github.io/repo/`)
needs a `<base href>` rewrite, a `.nojekyll`, and a 404-as-SPA-fallback trick. A root domain
needs none of them.

## Testing traps

The suite being green means less than it appears. Three gaps account for most production
escapes — `references/testing-traps.md` has the detail and the guard tests to copy.

**Hand-registered handlers rot silently.** If a CQRS dispatcher resolves handlers from DI but
every test constructs them directly, a handler nobody registered passes the whole suite and
throws on the first real request. Add a test that reflects over the assembly and asserts every
`ICommand<T>` / `IQuery<T>` has a registration.

**SQLite and PostgreSQL disagree.** SQLite cannot `ORDER BY` or compare a `DateTimeOffset`, and
it stores timestamps exactly where Postgres keeps microseconds — so equality on a timestamp
passes in tests and matches nothing live. Npgsql also refuses a non-zero UTC offset outright.

**Seeded state skips the path users take.** A helper that inserts an `Accepted` friendship row
means the send-then-accept flow is never executed. At least one test per flow should go through
the real commands end to end.

A guard test must be verified by breaking it on purpose. One that cannot fail is worse than
none, because it manufactures confidence.

## Secrets

Never in the repository, never pasted into chat. Local development uses
`dotnet user-secrets` and a gitignored `.env` for Docker Compose; deployed environments use the
host's own dashboard. If a credential does reach a transcript or a commit, rotate it rather than
deleting the message — assume it was captured.

When a managed host offers a connection string, let the host hold it. The developer machine
does not need production credentials, and not having them removes a whole category of accident.

## Bundled files

**`references/`** — read as needed:
- `scaffolding.md` — solution layout, dispatcher, EF conventions, ledger/cache patterns
- `blazor-pwa.md` — service worker, runtime config, cache headers, PWA checks
- `hosting.md` — Cloudflare Pages, Render, Neon, in setup order
- `testing-traps.md` — the guard tests, with the reasoning

**`assets/`** — copy and adapt; `MyApp`, `myapp` and `my-pages-project` stand in for your
names (plain identifiers, so the YAML stays valid as-is):
- `service-worker.published.js` — network-first worker, drop-in replacement
- `_headers`, `_redirects` — Cloudflare cache rules and SPA fallback
- `render.yaml` — API blueprint with build filters and health check
- `docker-compose.yml` — local Postgres
- `workflows/cloudflare-pages.yml` — build, point at the API, re-stamp hashes, verify live
- `workflows/ci.yml` — build and test on every push

**`scripts/`**:
- `fix_service_worker_integrity.py` — re-stamp hashes after post-publish edits
- `smoke_test.py` — template for driving the live API end to end

## Working habits that matter more than the stack

Explain *why* in comments, not *what*. A comment saying a foreign key is `Restrict` rather than
`Cascade` deliberately, and what breaks without it, survives contact with the next person. One
restating the code does not.

Write the commit message for whoever hits the same problem in six months. "Fix bug" costs them
an hour of `git log`.

Keep a build log — decisions, what went wrong, and what the symptom looked like before you
understood it. The wrong theories are worth recording: they are what makes the right one
teachable.
