# Life Achievement Unlocker

Your real life, scored like a video game. Do things, unlock achievements, earn XP, level up.

**Live:** https://sarovarks.github.io/LifeAcheivements/

Frontend on GitHub Pages, API on Render, PostgreSQL on Neon - all on free tiers. The API
sleeps after 15 minutes idle, so the first visit after a quiet spell takes about a minute to
wake. See [FREE-HOSTING.md](FREE-HOSTING.md).

**Phase 1** is accounts, the achievement catalogue, the claim flow, XP and levels, hidden
achievements, progress tracking, unlock notes, and the mobile-first Blazor PWA that puts a
trophy screen in front of all of it.

**Phase 2** is the social half: friends, an activity feed with reactions, friend verification
of claims, and a weekly leaderboard.

**Phase 3** is shipping it: both apps are containerised and CI runs the tests and builds the
images on every push. Two hosting paths are wired up:

- [FREE-HOSTING.md](FREE-HOSTING.md) - GitHub Pages + Render + Neon, no card, no monthly bill.
- [DEPLOYMENT.md](DEPLOYMENT.md) - Azure Container Apps and managed PostgreSQL, via
  `infra/main.bicep`.

Nothing is deployed yet; both need accounts that only you can create.

---

## Running it

You need the **.NET 10 SDK** and **Docker Desktop**.

### 1. Set the local credentials

No credentials are committed to this repository, so a fresh clone needs two one-off steps.

Database credentials for the container come from a `.env` file, which is gitignored:

```powershell
Copy-Item .env.example .env
```

Open `.env` and pick a password. Then tell the API how to reach that database. The connection
string lives in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets),
which are stored in your Windows user profile, not in the repository:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=lifeachievements;Username=lifeachievements;Password=<the password from .env>" --project src/LifeAchievements.Api
```

If you skip this, the API fails at startup with a message telling you exactly this command.

### 2. Start PostgreSQL

```powershell
docker compose up -d
```

Postgres listens on **localhost:5433** (not 5432, so it will not collide with a local install).

### 3. Run the API

```powershell
dotnet run --project src/LifeAchievements.Api
```

API on **http://localhost:5100**. On startup it applies EF migrations and syncs the 50-badge
catalogue, so a fresh clone needs no manual migration step. OpenAPI is at
`http://localhost:5100/openapi/v1.json` in Development.

### 4. Run the web app

In a second terminal:

```powershell
dotnet run --project src/LifeAchievements.Web
```

App on **http://localhost:5200**. Register, claim something, watch the XP land.

To install it as a PWA, open it in Chrome or Edge and use "Install app" / "Add to home screen".
(The service worker only caches aggressively in a `dotnet publish` build; in development it
stays out of the way so you are never served a stale app.)

### Tests

```powershell
dotnet test
```

139 tests: the level curve, streaks, progress windows and cooldown windows in `Domain.Tests`,
and the claim flow, daily cooldowns, progress, notes, friendships, verification, the feed,
reactions and the leaderboard against a real in-memory SQLite database in `Application.Tests`.

---

### Everything in containers

The same images that deploy, on your machine - web on 8080, API on 8081:

```powershell
docker compose -f docker-compose.full.yml up -d --build
```

## Solution layout

```
LifeAchievements.slnx                     (.NET 10's XML solution format)
├── docker-compose.yml                    PostgreSQL 17 for development (reads .env)
├── docker-compose.full.yml               the whole stack in containers
├── .env.example                          template for the gitignored .env
├── DEPLOYMENT.md                         containers, CI and Azure
├── FREE-HOSTING.md                       GitHub Pages + Render + Neon
├── render.yaml                           Render blueprint for the API
├── .github/workflows/                    ci.yml (tests + images), deploy.yml (Azure)
├── infra/main.bicep                       Container Apps + managed PostgreSQL
├── src/
│   ├── LifeAchievements.Domain           entities, enums, XP/level rules, domain events
│   ├── LifeAchievements.Application      commands, queries, handlers, validators, dispatcher
│   ├── LifeAchievements.Infrastructure   DbContext, EF configs, migrations, achievement seed
│   ├── LifeAchievements.Api              endpoints, Identity auth, DI, ProblemDetails, CORS
│   ├── LifeAchievements.Web              Blazor WebAssembly PWA
│   └── LifeAchievements.Shared           DTOs shared by Api and Web
└── tests/
    ├── LifeAchievements.Domain.Tests
    └── LifeAchievements.Application.Tests
```

Dependencies point inwards: `Api -> Infrastructure -> Application -> Domain`. The Application
layer talks to the database only through `IAppDbContext`, which is why the claim-flow tests can
swap Postgres for SQLite without touching a single handler.

---

## API

| Method | Route | What it does |
|---|---|---|
| `POST` | `/auth/signup` | Create an account **with a display name**, then call `/auth/login` |
| `POST` | `/auth/login` | Identity bearer token + refresh token |
| `POST` | `/auth/refresh` | Exchange a refresh token for a fresh access token |
| `GET` | `/me` | Display name, total XP, level, level title, XP to next level, progress %, unlocked count, streak |
| `GET` | `/achievements?category=` | Whole catalogue with my status on each; hidden ones masked until earned |
| `POST` | `/achievements/{id}/claim` | Claim one. Returns `UnlockResultDto` |
| `POST` | `/achievements/{id}/progress` | Log a step towards a multi-step achievement |
| `PUT` | `/me/unlocks/{id}/note` | Attach a line about what actually happened |
| `GET` | `/me/unlocks?take=10` | Recent approved unlocks, newest first |
| `GET` | `/friends` | Friends, plus requests waiting on either side |
| `POST` | `/friends/requests` | Add a friend by their exact email |
| `POST` | `/friends/requests/{id}` | Accept or decline a request |
| `DELETE` | `/friends/{userId}` | Remove a friend |
| `GET` | `/friends/leaderboard` | XP earned since Monday, you and your friends |
| `GET` | `/feed?take=30` | Recent unlocks from you and your friends |
| `POST` | `/feed/{id}/reactions` | Cheer or Respect an unlock (toggles) |
| `GET` | `/verifications` | Friends' claims waiting for somebody to vouch |
| `POST` | `/verifications/{id}` | Vouch for a friend's claim, or turn it down |

Everything except the auth routes needs `Authorization: Bearer <token>`.

ASP.NET Core Identity's built-in `/auth/register` is also mapped, but it only accepts an email
and a password. The game needs a display name from the first screen, so the web app uses
`/auth/signup` instead.

Errors come back as `ProblemDetails`. A broken game rule is a `400` whose `detail` is written to
be shown straight to the player ("Early Bird is cooling down. Try again in 7h 12m.").

---

## How the game works

### XP is a ledger

Every XP change is a row in `xp_transactions`. `users.TotalXp` is a **cached sum** of that
ledger, written in the same database transaction as the row that caused it. Nothing ever adds XP
by touching `TotalXp` alone, which means the ledger can always be replayed to audit a total.
The tests assert this invariant after every scenario.

### Levels are computed, never stored

`LevelCurve` in the Domain layer owns the whole curve: a threshold table to level 20, then a flat
2000 XP per level beyond it, plus the title bands (NPC → Side Quest Enjoyer → Side Character →
Supporting Cast → Main Character → Protagonist → Raid Leader → Final Boss). Re-tuning the game
means editing one file; no migration, no backfill.

### The claim flow

`ClaimAchievementCommand` is the heart of it:

1. The achievement must exist, and must not be hidden — hidden ones unlock themselves.
2. Not repeatable and already approved → friendly refusal.
3. Repeatable with a cooldown → the cooldown is enforced (Early Bird is once per day).
4. `VerificationType.Self` approves on the spot; `Friend` saves as **Pending** and pays nothing
   until Phase 2 adds verification.
5. On approval: write the `XpTransaction`, update `TotalXp`, raise `AchievementUnlocked` — one
   transaction, all or nothing.
6. The event handler evaluates every hidden-achievement rule and auto-unlocks the ones that now
   hold, through the same ledger.
7. The response carries old/new level, whether you levelled up, and any hidden achievements the
   claim triggered, so the unlock screen can show the whole payoff at once.

### Repeatables and their cooldowns

22 achievements can be unlocked more than once. How they limit themselves matters more than it
sounds:

| Limit | Count | Meaning |
|---|---|---|
| `CooldownWindow.Day` | 14 | Once per local calendar day - 10K Steps, Hydration Hero, Dear Diary |
| `CooldownWindow.Week` | 5 | Once per local calendar week, Monday to Sunday - Inbox Zero, Reconnected |
| `CooldownHours` | 3 | A rolling wait, where the gap itself is the point - Leg Day Survivor (72h) |

The daily ones are calendar-based rather than a rolling 24 hours on purpose. A rolling wait
measured from the last claim drifts: log 10K Steps at 9pm, walk again at 8am, and a 24-hour
cooldown still has 13 hours to run even though you genuinely did it on a new day. Worse, it
disagrees with `StreakCalculator`, which counts distinct local days - so the cooldown could
block a claim on a day you earned it and silently break the streak. Day and Week windows keep
the two rules in step.

### Progress, for the things that take more than a tap

Most achievements are a single act: you did it or you did not. Six of them are not, and those
carry a `TargetCount`, a `ProgressUnit` and a `ProgressWindow`:

| Achievement | Target | Window |
|---|---|---|
| Iron Will | 5 gym days | week |
| Sweet Nothing | 7 sugar-free days | week |
| No Spend Week | 7 no-spend days | week |
| Digital Detox Weekend | 2 offline days | week |
| Library Card | 12 books | year |
| Turf Legend | 50 runs | never resets |

`POST /achievements/{id}/progress` adds a step. Reaching the target does **not** unlock
anything by itself - it moves the achievement from `InProgress` to `Claimable`, so the player
still gets the trophy screen. Claiming cashes the run in and resets it to zero.

Windows are calendar-based rather than rolling, so "five gym days this week" means this week
and Monday genuinely starts fresh. A lapsed run reads as zero from the list endpoint without
writing anything; it is only reset for real when something next writes to it.

### Notes, so the app is worth reopening

Every unlock can carry one line of up to 280 characters - where you went, what it was like.
The note is set after the claim, from the unlock screen, so the trophy moment stays instant.
Notes show up again in recent unlocks on the home screen. The XP is the score; this is the
part you will actually want to read in a year.

### Hidden achievements

Six of them, unlocked by rules rather than by tapping anything. They are returned from
`/achievements` masked — name "Hidden achievement", hint only, no XP — until you earn them.

| Code | Rule |
|---|---|
| `FIRST_STEPS` | Your very first unlock |
| `NIGHT_OWL` | Claim something between 2 and 5 AM |
| `DEJA_VU` | 30 unlocks of the same repeatable achievement |
| `OVERACHIEVER` | 5 unlocks inside one calendar day |
| `WELL_ROUNDED` | At least one unlock in every category |
| `COMEBACK_KID` | Back after 14+ quiet days |

The rules live in `HiddenAchievementRulesHandler`; the thresholds live in
`Domain/Rules/HiddenAchievements.cs` so the handler and the seed can never drift apart.

### Streaks

Consecutive days with at least one approved unlock, counted back from today. Computed, not
stored. Yesterday still counts, so the streak does not die the moment midnight passes.

### Seeding

50 achievements across Health (10), Work (10), Fun (10), Social (8), Mind (6) and Hidden (6).
Ids are derived from the achievement `Code` with a hash, so they are identical on every machine
and re-seeding is idempotent: new badges are added, copy and XP changes are refreshed, and
nobody ever loses an unlock because a description was reworded.

---

## Notes on the frontend

The web project is Blazor WebAssembly, so it is worth saying what that means here.

- **No component library.** Plain CSS with the design tokens as CSS custom properties in
  `wwwroot/css/app.css`. MudBlazor and friends would fight the custom trophy-screen look.
- **Auth lives in localStorage.** WebAssembly has no server-side session, so `TokenStore` keeps
  the bearer and refresh tokens in the browser and `TokenAuthStateProvider` tells Blazor's
  `<AuthorizeView>` whether anyone is signed in. The API is the real gate; the UI only uses this
  to decide which screen to show.
- **One API client.** Every call goes through `GameApiClient`, which attaches the token,
  refreshes it a minute before it expires, and turns a `ProblemDetails` body into a message
  worth showing. A `401` clears the session and the router bounces you to login.
- **Icons are inline SVG.** `Components/Icon.razor` maps a string key (`"sunrise"`,
  `"dumbbell"`) to a stroke path, so the achievement seed can name an icon as plain data. No
  emoji, no icon font to download.
- **The unlock screen is a component, not a route.** It renders as a full-screen overlay over
  whatever you were on, so closing it puts you back exactly where you were.

---

## Secrets

Nothing secret is committed. The database password lives in `.env` (gitignored, used by
docker-compose) and the API's connection string lives in user secrets, keyed by the
`UserSecretsId` in `LifeAchievements.Api.csproj`. User secrets are loaded automatically in the
Development environment only; anywhere else the connection string has to come from the host,
for example the environment variable `ConnectionStrings__Default`.

To see or change what is stored:

```powershell
dotnet user-secrets list --project src/LifeAchievements.Api
```

## What is not here yet

Phase 2: friends, the activity feed, friend verification of pending claims, reactions and the
weekly leaderboard. The data model already has the shape for it — `UserAchievement` carries
`VerifiedByUserId`, friend-verified claims already sit in `Pending`, and the Friends tab is
wired up with an honest placeholder.
