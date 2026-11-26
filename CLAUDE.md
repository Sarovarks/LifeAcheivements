# Life Achievement Unlocker — project brief

Your real life, scored like a video game. Users do things in real life, unlock achievements (badges + XP), level up, and later share progress with friends.

This file is the source of truth for decisions made during planning. Build Phase 1 first (see bottom). Ask before deviating from these decisions.

## About the developer

- Backend-focused .NET developer (~5–6 years): C#, Clean Architecture, CQRS, EF Core, SQL Server.
- Not a frontend/React developer and doesn't know TypeScript — that's why the frontend is Blazor.
- Explain frontend/Blazor-specific choices briefly when you make them.

## Tech stack

| Layer | Choice | Notes |
|---|---|---|
| Frontend | Blazor WebAssembly, configured as a PWA | Installable on phone ("Add to home screen"). Plain CSS, no component library (MudBlazor etc. would fight the custom look). |
| Backend | ASP.NET Core Web API, latest LTS .NET installed on the machine | Clean Architecture + CQRS |
| CQRS | Small hand-written command/query dispatcher, or MediatR | Check MediatR's licence before using newer versions. |
| Database | PostgreSQL via Docker Compose locally | EF Core (Npgsql). SQL Server is an acceptable alternative if asked. |
| Auth | ASP.NET Core Identity with built-in API endpoints (bearer tokens) | Email + password in v1. Google sign-in later. |
| Validation | FluentValidation in Application layer | |
| Tests | xUnit for Domain and Application | Focus on XP/level rules and claim flow. |

No microservices, queues, Redis or caching in Phase 1.

## Solution structure

```
LifeAchievements.sln
├── docker-compose.yml                    → PostgreSQL
├── src/
│   ├── LifeAchievements.Domain           → entities, enums, domain events, XP/level rules
│   ├── LifeAchievements.Application      → commands, queries, handlers, validators
│   ├── LifeAchievements.Infrastructure   → DbContext, EF configs, migrations, seed data
│   ├── LifeAchievements.Api              → endpoints, auth, DI, middleware, CORS
│   ├── LifeAchievements.Web              → Blazor WASM PWA
│   └── LifeAchievements.Shared           → DTOs shared by Api and Web
└── tests/
    ├── LifeAchievements.Domain.Tests
    └── LifeAchievements.Application.Tests
```

## Data model

```
Users            Identity user + DisplayName, TotalXp (cached sum of ledger), CreatedAt

Achievements     Id, Code (unique, e.g. "EARLY_BIRD"), Name, Description, Category, Tier,
                 XpReward, IsHidden, Hint, IsRepeatable, CooldownHours (nullable),
                 VerificationType (Self / Friend / Auto)

UserAchievements Id, UserId, AchievementId, Status (Pending / Approved / Rejected),
                 ClaimedAt, ApprovedAt, VerifiedByUserId (nullable)

XpTransactions   Id, UserId, Amount, Reason, SourceUserAchievementId (nullable), CreatedAt

Friendships      Id, RequesterId, AddresseeId, Status (Pending / Accepted), CreatedAt   ← Phase 2
Reactions        Id, UserAchievementId, UserId, Type (Cheer / Respect), CreatedAt      ← Phase 2
```

Key decisions:

- **XP is a ledger.** Every XP change is an `XpTransactions` row. `Users.TotalXp` is a cached sum updated in the same transaction.
- **Levels are not stored.** Computed in Domain from TotalXp via thresholds. Titles by level band: 1 "NPC", 5 "Side Character", 10 "Main Character", 20 "Final Boss" (fill sensible titles in between). Keep the curve in one place so it can be tuned.
- **Streak** = consecutive days with at least one approved unlock (computed, not stored, in v1).

## Enums

- Category: Health, Work, Fun, Social, Mind
- Tier: Common, Rare, Legendary (XP roughly: Common 30–75, Rare 100–250, Legendary 400+)
- VerificationType: Self, Friend, Auto (Phase 1 only implements Self; Friend ones can be claimed but stay Pending)

## Achievement catalogue (seed data)

Target ~50 for v1. Write good, slightly funny descriptions — the copy is a big part of the fun.

| Category | Count | Examples |
|---|---|---|
| Health | 10 | Iron Will (gym 5 days in a week, 200 XP), Early Bird (woke before 6 AM, 50 XP, repeatable, once per day), 10K Steps, Hydration Hero |
| Work / learning | 10 | Friday Survivor (fixed a prod bug Friday evening and survived, 150 XP), Page Turner (finished a book, 120 XP), Inbox Zero, New Skill Unlocked |
| Fun / lifestyle | 10 | First Day First Show (watched a film on release day, 75 XP, friend-verified), Home Chef (cooked instead of ordering, 40 XP, repeatable), Solo Trip, Sunset Chaser |
| Social | 8 | Turf Legend (50 runs in Sunday turf cricket, friend-verified), Reconnected (called an old friend), Party Starter |
| Mind / habits | 6 | Monk Mode (24h no social media, 100 XP, repeatable), Digital Detox Weekend, No Spend Week |
| Hidden | 6 | Night Owl (claim any achievement after 2 AM), Déjà Vu (30th unlock of the same repeatable), + 4 more surprises |

Include a few Legendary long-term goals (half marathon, 12 books in a year).

Hidden achievements: list endpoint returns them masked (name "Hidden achievement", hint only, no XP shown) until unlocked. They are unlocked automatically by rules, not claimed.

Seed via EF Core `HasData` or a seeding service keyed on `Code` (idempotent).

## API (Phase 1)

```
POST /auth/register, /auth/login, /auth/refresh   (Identity API endpoints)

GET  /me                        → display name, TotalXp, level, level title, XP to next level,
                                  progress %, unlocked count, streak
GET  /achievements?category=    → all achievements with my status on each
                                  (Locked / Claimable / Pending / Unlocked + times unlocked);
                                  hidden ones masked unless unlocked
POST /achievements/{id}/claim   → returns UnlockResultDto
GET  /me/unlocks?take=10        → recent approved unlocks
```

Phase 2 (do not build yet): friends, feed, verify/reject claims, reactions, weekly leaderboard.

## Claim flow (core logic)

`ClaimAchievementCommand`:

1. Achievement exists and is not hidden (hidden can't be claimed manually).
2. If not repeatable and already approved → reject with a friendly error.
3. If repeatable with a cooldown → enforce it (e.g. Early Bird once per day).
4. VerificationType Self → approve immediately. Friend → save as Pending, return pending result.
5. On approval: add XpTransaction, update TotalXp, raise `AchievementUnlocked` domain event — all in one DB transaction.
6. Event handlers: detect level-up; evaluate hidden achievement rules (Night Owl, Déjà Vu, …) and auto-unlock them (also via ledger).
7. Return `UnlockResultDto`: achievement info, XpGained, NewTotalXp, OldLevel, NewLevel, LeveledUp, BonusUnlocks (hidden ones triggered).

Unit-test this flow thoroughly.

## Visual design

Dark, game-like "trophy screen" feel — not a boring habit tracker. Mobile-first, phone width ~390px.

Colours (CSS variables):

```
--bg:        #0E1020   page background
--surface:   #181B30   cards, bottom nav
--surface-2: #22264A   inner tiles, icon squares
--border:    #2C3160
--text:      #F2F3FA
--muted:     #A3A8C8
--gold:      #F5B83D   XP, rewards, primary buttons, active nav (text on gold = #0E1020)
--teal:      #4FD1C5   streaks, progress, "done" states
```

Typography: headings "Space Grotesk" (500/700), body "Inter" (400/500/600) from Google Fonts.
Shapes: cards radius 18–24px, icon squares 12–14px, pills 18–20px. Touch targets ≥ 44px.
Icons: outline/stroke style (e.g. an icon set or inline SVG). No emoji in the UI.

Bottom nav (all main screens): Home, Badges, big raised gold "+" button (claim), Friends, Profile. Active item in gold.

### Screens

1. **Home** — greeting + name, notifications button. Profile card: circular level ring (gold progress) with level number, level title, "XP to next level", total XP; three stat tiles (Unlocked, Day streak in teal, Friends rank). "Today's quests": in-progress achievements with progress bars. "Recent unlocks": icon, name, time, "+XP" in gold.
2. **Achievements** — title + "X of Y unlocked · N hidden". Category filter chips (All, Health, Work, Fun, Social), selected chip gold. Cards: icon square, name, short description, and on the right either a gold "+XP" claim button, a teal "Done ×N" for repeatables, "Friend verifies" label, or a dashed-border locked card for hidden ones with a hint and "???".
3. **Unlock moment** (after a successful claim) — close button, "ACHIEVEMENT UNLOCKED" in gold caps, big tilted gold badge with a few confetti dots inside a dashed ring, achievement name large, description, "+XP" pill and rarity pill, level progress bar, buttons "Share with friends" (gold, Phase 2) and "Back to home". Show a level-up message if LeveledUp. Subtle entrance animation is welcome.
4. **Friends** — Phase 2. Weekly podium (top 3), activity feed with Cheer/Respect buttons, "verify my friend's claim" cards.
5. **Login / Register** — same dark style, simple.

## Phase 1 scope (build this now)

- [ ] Solution + projects as above, docker-compose for PostgreSQL, README with run steps
- [ ] Domain: entities, enums, level/XP rules, domain events
- [ ] Infrastructure: DbContext, configurations, initial migration, achievement seed (~50)
- [ ] Application: claim command, /me, achievements list, recent unlocks queries, hidden-achievement rules
- [ ] Api: Identity auth endpoints, the Phase 1 endpoints, CORS for the Blazor app, ProblemDetails errors
- [ ] Web: Blazor WASM PWA with Login/Register, Home, Achievements, Unlock screens + bottom nav, styled per the design above
- [ ] Tests: level calculation, claim flow (non-repeatable, cooldown, pending, hidden auto-unlock, level-up)

Done means: `docker compose up`, run API and Web, register, claim achievements, see XP/level update and the unlock screen.

## Later phases (for context only)

2. Social: friends, feed, friend verification, reactions, weekly leaderboard.
3. Ship: Dockerize, deploy to Azure (Container Apps or App Service + managed Postgres), GitHub Actions CI/CD.
4. Grow: auto-unlocks via background jobs, push notifications, Redis for leaderboard, custom group achievements, a queue (RabbitMQ / Service Bus) where it fits.
