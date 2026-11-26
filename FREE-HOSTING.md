# Hosting this for free

Three services, no card, no monthly bill:

| Piece | Where | Free because |
|---|---|---|
| Blazor frontend | **GitHub Pages** | Static files from a public repo |
| API | **Render** | Free web service, 750 hours a month |
| PostgreSQL | **Neon** | Free plan, 1 GB storage |

The one real catch: **Render's free tier spins down after 15 minutes idle** and takes about a
minute to wake. The first visit after a quiet spell sits on the loading spinner while the API
boots; everything after that is normal speed. Neon also sleeps after 5 minutes but wakes in
well under a second, so you will not notice that one.

Everything below is already wired up in this repository. What is left is creating the two
accounts and pasting three values.

---

## Status

| Piece | State |
|---|---|
| Frontend | **Live** - https://scoremylife.pages.dev (Cloudflare Pages) |
| API | **Live** - https://lifeachievements-api.onrender.com |
| Database | **Live** - Neon, Singapore, free plan |

Verified end to end on the live site: registered an account, claimed Half Marathon, levelled
1 -> 3, and the hidden First Steps fired. The result is in Neon - 550 XP, 2 unlocks.

### Two things that caught us out

**GitHub Pages has to be enabled by hand the first time**: Settings -> Pages -> Source ->
"GitHub Actions". The workflow's `enablement: true` is not permitted to do it with the
default token, and until it is done every run fails at `configure-pages`.

**GitHub Pages sends `Cache-Control: max-age=600` on everything**, including
`appsettings.json` and `css/app.css` - neither of which carries a fingerprint in its name, so
a cached copy hides the change completely. The nginx rules in this repository that mark that file no-cache apply to
the Docker image, not to Pages. So for up to ten minutes after a deploy that changes the API
address, a browser that visited earlier keeps the old one and the app quietly calls the wrong
host. A hard reload clears it; otherwise it sorts itself out within ten minutes. Worth
remembering before assuming a deploy is broken.

## Moving the frontend to Cloudflare Pages

GitHub Pages works, but it serves the app from `/LifeAcheivements/` and forces
`Cache-Control: max-age=600` on everything. Cloudflare Pages gives a root URL and lets the
cache headers be set properly, which removes three workarounds and one real bug.

### What you do

1. Sign up at [dash.cloudflare.com](https://dash.cloudflare.com) - free, no card.
2. **Workers & Pages -> Create -> Pages -> Upload assets**, name the project
   **`scoremylife`**, and upload anything (a single empty file is fine) just to create
   it. The GitHub workflow replaces the contents on the first real deploy.
3. **My Profile -> API Tokens -> Create Token -> Edit Cloudflare Workers** (or a custom token
   with the *Cloudflare Pages: Edit* permission). Copy it.
4. Copy your **Account ID** from the right-hand side of any dashboard page.
5. Add both as repository **secrets** (not variables - these are sensitive) at
   `Settings -> Secrets and variables -> Actions -> Secrets`:

| Secret | Value |
|---|---|
| `CLOUDFLARE_API_TOKEN` | the token from step 3 |
| `CLOUDFLARE_ACCOUNT_ID` | the id from step 4 |

6. **Actions -> Deploy frontend to Cloudflare Pages -> Run workflow.**

The site lands at `https://scoremylife.pages.dev`.

### What the move fixes

Serving from a root domain rather than a `/repo/` subpath makes three GitHub Pages
workarounds unnecessary: no `<base href>` rewrite, no `.nojekyll`, no `404.html`. The SPA
fallback becomes a proper 200 via `wwwroot/_redirects` instead of a 404 whose body happens to
boot the app.

More importantly, `wwwroot/_headers` marks `appsettings.json`, `index.html` and the service
worker as `no-store`, so a deploy that changes the API address takes effect immediately
rather than up to ten minutes later.

One trap worth knowing: Cloudflare treats `_redirects` and `_headers` as build configuration
and does not serve them. Blazor lists every published file in the service worker manifest, so
the worker would try to cache two files that return 404, fail its install, and leave the app
quietly uninstallable. The deploy excludes them from the manifest.

### After it works

`render.yaml` already allows both origins. Once nothing points at the github.io address, drop
`Cors__AllowedOrigins__0` and leave only the Cloudflare one, and set the GitHub Pages workflow
to `workflow_dispatch` only so the two do not both deploy on every push.

## 1. Database: Neon

1. Sign up at [neon.tech](https://neon.tech) with your GitHub account.
2. Create a project. Any region near you - `ap-southeast-1` (Singapore) is the closest to India.
3. Name the database `lifeachievements`.
4. Copy the connection details.

Neon hands you a URI. Npgsql wants key/value form, so reshape it:

```
Host=ep-something-123456.ap-southeast-1.aws.neon.tech;Database=lifeachievements;Username=neondb_owner;Password=YOUR_PASSWORD;SSL Mode=Require
```

Keep that somewhere safe. It goes into Render in the next step and nowhere else - never into
this repository.

---

## 2. API: Render

1. Sign up at [render.com](https://render.com) with GitHub.
2. **New → Blueprint**, pick the `LifeAcheivements` repository.

Render reads [`render.yaml`](render.yaml) and sets up the service: Docker runtime, free plan,
Singapore, health check on `/health/live`. The only thing it will ask for is the one value
marked `sync: false`:

| Setting | Value |
|---|---|
| `ConnectionStrings__Default` | the Neon string from step 1 |

Deploy. The first build takes a few minutes - it is compiling .NET inside Docker. When it
finishes you get a URL like `https://lifeachievements-api.onrender.com`.

Check it:

```powershell
curl https://lifeachievements-api.onrender.com/health/ready
```

`Healthy` means it reached Neon, applied all migrations and seeded the 50 achievements.

---

## 3. Frontend: GitHub Pages

1. Repository **Settings → Pages → Source → GitHub Actions**.
2. Repository **Settings → Secrets and variables → Actions → Variables → New**:

| Variable | Value |
|---|---|
| `API_BASE_URL` | your Render URL, no trailing slash |

3. **Actions → Deploy frontend to GitHub Pages → Run workflow.**

It lands at `https://sarovarks.github.io/LifeAcheivements/`.

---

## 4. Let the API trust the frontend

`render.yaml` already sets `Cors__AllowedOrigins__0` to `https://sarovarks.github.io`, which is
correct if you keep the repository name. If you rename it the origin does not change - CORS
cares about scheme and host only, never the path - so there is nothing to update.

If you ever move the frontend elsewhere, change that variable in Render and redeploy.

---

## What the workflow does that you would otherwise hit as bugs

Blazor on GitHub Pages has four traps, all handled in
[`.github/workflows/pages.yml`](.github/workflows/pages.yml):

1. **Pages serves a project site from `/LifeAcheivements/`**, not the root, so `<base href>` is
   rewritten at publish.
2. **Jekyll silently drops folders starting with an underscore** - which is where Blazor puts
   `_framework`. A `.nojekyll` file disables it. Without this the page loads and the runtime
   404s.
3. **Pages has no SPA rewrite**, so `index.html` is copied to `404.html`. A deep link like
   `/friends` then returns a 404 status whose body boots the app, which routes on the URL.
   Odd, but it is the standard trick and it works.
4. **The service worker records a SHA-256 for every file.** Editing `index.html` and
   `appsettings.json` after publish leaves those hashes stale, the integrity check fails, and
   the app quietly stops being installable while still appearing to work.
   [`fix_service_worker_integrity.py`](.github/scripts/fix_service_worker_integrity.py)
   re-stamps them.

Trap 4 was found by checking rather than assuming: the published hash for `index.html` did not
match after the base-href rewrite.

---

## Verified, and not

**Verified locally:**

- The frontend publishes, the base href rewrite applies, and the app boots and routes when
  served from a `/LifeAcheivements/` subpath.
- The SPA fallback works: a deep link returns `404.html`, the app boots and routes.
- All 69 service worker hashes match after the re-stamp.
- The API binds to `PORT` (`Now listening on: http://[::]:10000`), which is how Render decides
  the service is up.
- `/health/live` and `/health/ready` both answer from inside the container.

**Not verified:**

- Service worker registration, so PWA installability. The browser used for testing blocks
  service workers entirely - the unmodified Docker build fails the same way at the root path,
  which rules the Pages changes out as the cause, but it does not prove the PWA installs.
  Worth checking in a normal browser once it is live: DevTools → Application → Service Workers.
- Anything on Render or Neon, since neither account exists yet.

---

## Free tier limits worth knowing

| | Limit |
|---|---|
| Render free | 750 instance-hours/month, spins down after 15 min idle, ~1 min cold start, ephemeral disk |
| Neon free | 1 GB storage, 100 CU-hours/project, scale to zero after 5 min |
| GitHub Pages | 1 GB site, 100 GB/month soft bandwidth limit |

The ephemeral disk on Render does not matter here - all state is in Neon.

These change regularly; check [render.com/docs/free](https://render.com/docs/free) and
[neon.com/pricing](https://neon.com/pricing) before relying on them.
