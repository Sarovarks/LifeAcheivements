# Build log

What was built, what was decided and why, and what went wrong on the way. Written at the end
of each day, while the reasoning was still fresh.

- **26 November 2026** — empty folder to live app: 24 commits, 139 tests.
- **27 November 2026** — a day of using it rather than building it: 12 commits, 156 tests.

Totals: 37 commits, 166 files, ~12,100 lines of C# and Razor.

| | |
|---|---|
| App | https://scoremylife.pages.dev |
| Fallback | https://sarovarks.github.io/LifeAchievements/ |
| API | https://lifeachievements-api.onrender.com |
| Database | Neon PostgreSQL, Singapore |
| Repository | https://github.com/Sarovarks/LifeAchievements |
| Cost | Nothing |

---

## Phase 1 — the game

Clean Architecture with CQRS, as the brief asked: `Api -> Infrastructure -> Application ->
Domain`, dependencies pointing inwards.

**Stack**: .NET 10 LTS, ASP.NET Core minimal APIs, EF Core with Npgsql, Blazor WebAssembly PWA,
PostgreSQL, xUnit. A hand-written command/query dispatcher rather than MediatR, so there is no
licence question hanging over it.

### The decisions that shaped everything after

**XP is a ledger.** Every XP change is a row in `xp_transactions`. `Users.TotalXp` is only a
cached sum, written in the same database transaction as the row that caused it. Nothing ever
adds XP by touching `TotalXp` alone. Every test asserts the two still agree, which is what made
the weekly leaderboard trivial later — it just sums the ledger over a date range.

**Levels are computed, never stored.** `LevelCurve` owns a threshold table to level 20 and a
flat 2,000 XP per level beyond it, plus the title bands: NPC → Side Quest Enjoyer → Side
Character → Supporting Cast → Main Character → Protagonist → Raid Leader → Final Boss.
Rebalancing the game is a one-file edit with no migration and no backfill.

**Streaks are computed too.** Consecutive local days with at least one approved unlock.
Yesterday still counts, so a streak does not die at midnight while you are asleep.

**Seeding is idempotent.** 50 achievements, ids derived from a hash of the achievement `Code`,
so they are identical on every machine. Re-seeding adds new badges and refreshes copy without
anyone losing an unlock.

### The claim flow

One command, `ClaimAchievementCommand`, carrying the rules: the achievement must exist and not
be hidden; one-shot achievements cannot be claimed twice; repeatables respect their cooldown;
friend-verified claims park as `Pending`; self-verified ones write the ledger, update the
cached total and raise `AchievementUnlocked` inside a single transaction. Handlers then
evaluate the six hidden-achievement rules and may award more, through the same ledger.

Hidden achievements are returned masked from the list endpoint until earned — name replaced,
hint only, no XP shown.

---

## The missing piece

With Phase 1 working, the question was asked: *does something feel missing?* It did, and
finding it mattered more than any feature after it.

Every achievement was a **single tap**. "Ran a half marathon" and "drank some water" were
mechanically identical: one tap, instant XP. The Home screen's progress bars were hardcoded:

```csharp
private static int QuestProgress(AchievementDto quest) =>
    quest.Status == AchievementStatuses.Claimable ? 100 : 35;   // decorative, meaningless
```

XP you can mint instantly is worth nothing, and some part of you knows it while tapping.

### Progress

Six genuinely multi-step achievements gained a target, a unit and a window:

| Achievement | Target | Window |
|---|---|---|
| Iron Will | 5 gym days | week |
| Sweet Nothing | 7 sugar-free days | week |
| No Spend Week | 7 no-spend days | week |
| Digital Detox Weekend | 2 offline days | week |
| Library Card | 12 books | year |
| Turf Legend | 50 runs | never resets |

Reaching the target does **not** unlock anything — it makes the achievement claimable, so the
trophy screen still belongs to the player. Claiming cashes the run in and resets it. Windows
are calendar-based, so a gym week really does end on Sunday.

### Notes

Every unlock takes one line of up to 280 characters, written on the unlock screen *after* the
claim so the trophy moment stays instant. *"Munnar. Got lost twice."* The XP is the score; the
note is the part worth reading in a year. It also turned out to be what made Phase 2's
verification feel like anything — vouching for "I went to the gym, trust me" is thin, vouching
for a friend's written account is not.

---

## Phase 2 — the social half

Friends, an activity feed with reactions, friend verification, and a weekly leaderboard.

**The refactor that came first.** Before writing verification, the approval pipeline was
extracted from the claim command into `UnlockService`. Both paths now share one piece of code:
the same ledger write, the same cached-total update, the same hidden-achievement evaluation,
the same level-up detection. Only `VerifiedByUserId` differs.

Writing that logic a second time inside the verify handler was the obvious way to end up with
friend-verified unlocks that quietly skip hidden achievements. The first live verification paid
out 125 XP — 75 for the achievement plus a 50 XP hidden bonus that fired correctly — which is
that decision paying off.

**Friends are added by exact email**, deliberately not by name search: a name search lets
anybody enumerate every user of the app, and nobody needs that to add three people they know.
One row per pair in whichever direction the request went. Two people who ask each other at the
same time become friends rather than piling up crossed requests.

**Hidden achievements never reach the feed.** Finding one should stay a private surprise.

**Cheer and Respect** are independent toggles, limited to your own and friends' unlocks.

**The leaderboard sums the ledger**, not `Users.TotalXp` — the cached total is lifetime XP and
says nothing about a week.

---

## Phase 3 — shipping it

Both apps containerised, CI on every push, and two hosting paths.

The Blazor app publishes to plain static files, so its runtime image is nginx at 103 MB rather
than a .NET one. The API image is 355 MB on the aspnet runtime, running as the non-root `app`
user.

**The config problem worth knowing about.** Blazor WebAssembly runs in the browser, so anything
baked in at publish time is fixed for the life of the image. Instead, the API address is
written into `wwwroot/appsettings.json` when the container starts, from an environment
variable. One image runs against local, staging or production without rebuilding.

**Health endpoints are split on purpose**: `/health/live` never touches the database, so a
database blip takes the app out of rotation rather than getting the container killed and
restarted in a loop. `/health/ready` checks Postgres too.

Azure infrastructure exists as `infra/main.bicep` — Container Apps, managed PostgreSQL, Log
Analytics, managed-identity registry pulls — and compiles cleanly. It was never deployed: the
only Azure login available was a work account with no subscription, and a personal side project
does not belong in an employer's tenant.

---

## Where it ended up running

Free hosting that actually works:

| Piece | Where | Why free |
|---|---|---|
| Frontend | Cloudflare Pages | Static files, root domain |
| API | Render | 750 instance-hours a month |
| Database | Neon | 1 GB, scales to zero |

No payment method is attached to any of them, which is a stronger guarantee than any plan
description: without a card they suspend at the limit rather than bill.

The one real cost is a **cold start**. Render's free tier sleeps after 15 minutes idle and
takes about a minute to wake, so the first visit after a quiet spell sits on the loading
spinner. Neon sleeps too but wakes in under a second.

The frontend moved from GitHub Pages to Cloudflare Pages for a root URL. That removed three
workarounds — no `<base href>` rewrite, no `.nojekyll`, no `404.html` SPA trick — and fixed a
real caching bug. GitHub Pages is kept as a live fallback, which proved its worth within hours.

---

## Bugs found, and how

The useful part of the day. Most of these look fine on the surface, which is the point.

### Npgsql rejects a non-zero UTC offset

`DateTimeOffset.Now` carries +05:30 here, and Npgsql refuses to write that to a `timestamptz`.
Every single write would have thrown on first contact with Postgres. Fixed with a value
converter that normalises to UTC on the way in, and day/hour rules that convert back to local
time — otherwise "2 AM" would have meant 2 AM UTC, which is 7:30 AM locally.

### Hidden achievements matched on a timestamp

Bonus unlocks were found by `ApprovedAt == now`. SQLite stores timestamps exactly, so the tests
passed; Postgres keeps microseconds, so the comparison would have silently returned nothing and
hidden achievements would have vanished from the unlock screen in production. The event now
carries the ids of what it awarded.

### Daily cooldowns drifted

"Once per day" was a rolling 24 hours from the last claim. Log 10K Steps at 9pm, walk again at
8am, and the claim was refused with 13 hours still to run — on a day it was genuinely earned.
Worse, it disagreed with the streak calculator, which counts calendar days, so a cooldown could
block the only claim of a day and break a streak the player had actually kept. Daily and weekly
repeatables now use calendar windows that line up with how streaks are counted.

### Stale service worker hashes

The GitHub Pages deploy rewrites `index.html` and `appsettings.json` after publish, which left
the SHA-256 hashes the service worker had recorded for them wrong. The integrity check fails,
and the app quietly stops being installable as a PWA while still appearing to work perfectly.
Found by checking the hashes rather than assuming. A script now re-stamps them.

### A service worker wedging users on a stale build

The symptom was "the site is down" — while `curl` returned 200 five times running and incognito
worked. Blazor's published service worker is cache-first, caches every asset with
`cache.addAll` (which rejects the whole install if one asset fails), and calls neither
`skipWaiting` nor `clients.claim`. So a worker installed on the first visit kept serving its own
cache across four deploys, broke, and produced `ERR_FAILED` before the network was ever touched.

Two wrong theories came first — a corporate proxy, then HTTP/3 — both plausible, both wrong.
The clincher was the deployment-specific URL working: same Cloudflare, same QUIC, different
origin, therefore no service worker. Assets now cache individually and a new worker takes over
immediately.

### A service worker that made every cache rule pointless

Found the next day, testing the live site. The worker was cache-first for every GET, and
`index.html`, `appsettings.json` and `css/app.css` were all in its precache. A service worker
answers before the network is consulted, so none of the `no-store` rules in `_headers` reached
anyone who had visited the site before — they kept running an old build, pointed at whichever
API address was current on their first visit. The caching work of the previous day was real and
correct and had no effect on returning visitors.

Now network-first with the cache as an offline fallback only, and cross-origin and non-GET
requests bypass the worker entirely. Nothing is slower: `fetch` inside the worker still honours
`Cache-Control`, and `_framework/*` is immutable, so those files never touch the network either
way. Offline support drops to the app shell, which is the honest ceiling for an app that can do
nothing without its API.

The general lesson is worth keeping: a service worker overrides HTTP caching rather than
cooperating with it, so cache headers and a cache-first worker are not two layers of the same
strategy — the worker simply wins.

### An `apt-get` that broke CI

`curl` was installed in the API image purely to give the container a `HEALTHCHECK` — which
nothing consumed, since Render probes `healthCheckPath` and the Azure template has its own
probes, both from outside the container. In exchange it made every image build depend on a
Debian mirror being reachable, and that broke CI with the same Dockerfile that had passed
minutes earlier. Removed; the build is hermetic again and the image is 9 MB smaller.

### GitHub Pages forces `max-age=600`

A deploy that changed the API address left browsers calling the old one for ten minutes. The
`no-cache` rules in `nginx.conf` apply to the Docker image, not to Pages. The symptom was
baffling — the app calling `http://localhost:5100` from a live HTTPS site — until the network
log showed the actual request URL. `css/app.css` had the same problem, hiding style changes,
since unlike the files under `_framework/` its name never changes. Both are fixed on Cloudflare
via `_headers`; GitHub Pages cannot be told, which is one more reason Cloudflare is primary.

### Category chips hid a filter

Six categories did not fit one phone row, so the row scrolled sideways and `Mind` sat
off-screen. Selecting it turned a chip gold where nobody could see it, and the filter looked
like it had done nothing. They wrap now.

---

## Day two — using it, not building it

Nothing below came from a plan. Every item was found by using the app, or by checking
something instead of assuming it.

### Sharing became a card

"Share with friends" was still `disabled` with `title="Coming in Phase 2"` — Phase 2 built the
friends feed and never came back to the unlock screen. The notifications bell was the same, and
the two were the last dead controls in the app.

Sharing now draws the unlock as a 1080x1350 PNG on a canvas — the tilted gold badge, the name,
the XP and tier pills, the note, the level bar — and hands that to the device's share sheet,
with text as the fallback rather than the goal. A badge reads at a glance in a chat; a line of
text scrolls past.

The badge icon is lifted out of the live DOM and redrawn into the canvas rather than restated
in JavaScript. The icons live in one `switch` in `Icon.razor`; copying them into JS would have
created a second icon set to keep in sync, and it would have drifted the first time a badge was
added.

Rendering it was worth more than reasoning about it. Two faults were obvious on sight and
invisible in code: the bottom third was crushed together while the top had room to spare, and a
long note was truncated with space left over. The progress block is pinned to a fixed height
now, so every card has the same footing, and the note takes whatever is left between the pills
and that block — a long achievement name costs the note a line instead of colliding with the
bar.

The label was also half a lie, which mattered more than the dead button: an approved unlock is
in the friends feed the moment it is claimed. A caption under the button says so now, because
otherwise the share sheet implies the opposite.

### The bell became real

Three things already happened without telling anyone: someone reacted to your unlock, someone
asked to be your friend, or a friend's claim sat waiting on your word. The only way to find out
was to wander to the Friends tab and notice a number had changed.

Nothing is stored. The list is read from the rows that already exist, so there is no second
copy of the truth to drift and no table to prune. The only new state is one nullable column,
`LastSeenNotificationsAt`, which is what makes the badge mean "new" rather than "ever".

Reactions you left on your own unlock are excluded. The rule added earlier the same day forbids
making new ones, but rows from before it are still in the live database — without that filter
the first notification would have been you, congratulating yourself.

### Self-cheering

Reacting to your own unlock was allowed. The rule only excluded strangers:

```csharp
if (claim.UserId != userId && !await FriendQueries.AreFriends(...))
```

Your own id passes straight through that condition. It was never a decision — it fell out of a
check aimed at something else. A cheer count you can raise yourself means nothing, so the
handler refuses it and your own unlocks show a plain tally instead of buttons.

### A friend who could not see the feed

Reported as a bug, and it was not one. The application layer was correct, which the new
end-to-end friendship test proved, and a live reproduction on production confirmed: an accepted
friend sees the unlock and can cheer it.

The real cause was the stale cache-first service worker from the previous day, still serving an
old build on that device until every tab was closed. The fix could not reach the browser that
needed it, which is the nature of that bug.

Two real bugs were found while chasing a third that did not exist. That is the argument for
reproducing rather than theorising.

### Two bugs only production could show

**Nameless accounts.** Reproducing the feed against the live API returned `'Culinary Explorer'
by ` — no name at all. Identity maps `/auth/register` alongside the endpoints the app needs,
and it takes only an email and a password, so an account created through it has no display
name. The app uses `/auth/signup`, which sets one; `/auth/register` was a second, unguarded
door to a half-made account. The feed guarded this with `?? "Someone"`, which catches null — a
blank name is an empty string and sailed through.

Invisible to the tests, because the test helper always passes a name. Invisible in the app,
because the UI only ever calls `/auth/signup`. It took hitting production the way a stranger
would.

**An endpoint with no handler.** `/notifications` returned 500: *No handler registered for
GetNotificationsQuery*. Handlers are wired up by hand, one line each, and two were added
without wiring either. The whole suite passed over a dead endpoint, because every test
constructs handlers directly and none asks the container for one.

The two missing lines were the easy part. The gap worth closing was that nothing could have
caught it, which is what the registration test now does.

### The deploy pipeline had been dead for hours

Two backend commits sat undeployed while everything looked healthy: the API answered, the
dashboard was green, old deploys were listed. Pushes simply produced no event at all.

The repository had been renamed that morning — `LifeAcheivements` to `LifeAchievements`. The
rename itself was a settings change and a docs sweep, and the frontend was unaffected because
Cloudflare deploys through GitHub Actions inside the repository. Render reaches in from
outside, and its access did not survive.

The precise cause was narrower than "the link broke". On GitHub, Render appeared under
**Authorized GitHub Apps** but not under **Installed GitHub Apps**. Authorised means it may act
on your behalf — enough to sign you in and show a dashboard. Installed is what grants access to
repositories and sends the push webhooks. Render was one and not the other, so it never heard
about any push.

The lesson is the one about renaming: it is only free for the things that live inside the
repository. Anything reaching in from outside has a connection that may not follow.

**`/health/version` exists because of this.** It reports the running commit, the branch and the
instance start time from the variables Render sets. Before it, the only way to tell which build
was live was to exercise a behaviour that had changed in it — which needed a throwaway account
and a claim. A deploy that silently does not happen is invisible without it.

### Housekeeping

The repository name is spelled correctly now. The nine `@example.com` accounts created while
verifying all of the above were deleted from Neon in one transaction — friendship rows first,
because `friendships.AddresseeId` is `Restrict` rather than cascade and the delete fails
without it. Afterwards: no orphaned ledger rows, unlocks, reactions or friendships, and every
remaining player's `TotalXp` still equals the sum of their ledger.

### What the day was actually about

Of the four bugs that mattered, the test suite caught none. They were found by rendering the
card and looking at it, by calling the live API the way a stranger would, and by checking a
deploy instead of trusting it.

The tests were not useless — they made every fix safe to make. But a passing suite described a
system that could not register an endpoint, could not deploy, and was serving nameless users.
The habit worth keeping is the one that found them: check the thing itself, especially when it
looks like it worked.

---

## Tests

156, all green.

**Domain (55)** — the level curve at every boundary, title bands, progress windows, cooldown
windows, and streak counting.

**Application (101)** — the whole claim flow against a real in-memory SQLite database:
non-repeatable claims, cooldowns, pending friend verification, hidden auto-unlocks, level-ups,
progress blocking a premature claim, notes, friendships, verification, the feed, reactions and
the leaderboard.

Two assertions are worth keeping an eye on. One asserts the cooldown never blocks a day the
streak would have counted — the two rules have to agree. Another asserts the ledger equals the
cached total after every scenario.

Day two added two tests that check the tests themselves rather than the game. One drives a
friendship through the real `SendFriendRequest` and `RespondToFriendRequest` commands, because
every other social test started from a seeded `Accepted` row and the path two actual people
take had never once been executed. The other walks the Application assembly and fails if any
`ICommand<T>` or `IQuery<T>` has no handler registered in `AddApplication` — written after a
missing registration reached production. Both were verified by breaking them on purpose: a
guard that cannot fail is worse than no guard, because it manufactures confidence.

---

## Known limitations

- **Cold starts.** First request after 15 idle minutes takes about a minute.
- **Progress is self-reported.** Tapping "+1 Log" is still the honour system. What changed is
  the shape: five taps across a week, each a small decision, instead of one tap minting 200 XP.
- **The leaderboard and the notifications list sort in memory**, because SQLite cannot compare
  or order a `DateTimeOffset` and the tests run on it. Both sets are bounded by one person and
  their friends, so it costs little; anything global would want a UTC `DateTime` column and a
  database-side sort.
- **The API migrates on startup**, so it runs a single replica. Scaling out means moving
  migrations into the deploy step first.
- **PWA installability is unverified.** The browser used for testing blocks service workers, so
  it could not be confirmed. Worth a look in Chrome: DevTools → Application → Service Workers.
- **Notifications are in-app only.** The bell shows what happened while you were away; it
  cannot reach you when the app is closed. Real push needs a service worker subscription and
  VAPID keys, which is Phase 4.
- **A friend request disappears from the bell once answered.** Notifications are derived from
  the rows that already exist rather than stored, so answering a request removes the thing the
  notification was made from. Right, probably — but a consequence of the design, not a choice
  made separately.

---

## What Phase 4 would be

Auto-unlocks from background jobs, push notifications, Redis for the leaderboard, and a queue
where it fits. None of it is needed for the app to be good; all of it is in the brief as
later work.
