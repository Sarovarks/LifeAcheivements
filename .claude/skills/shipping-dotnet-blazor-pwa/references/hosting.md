# Free hosting, in setup order

Cloudflare Pages for the front end, Render for the API, Neon for Postgres. All three free, no
card on file — which matters more than the plan description, because without a payment method
they suspend at the limit rather than bill.

## Contents

- [Order of operations](#order-of-operations)
- [Neon: the database](#neon-the-database)
- [Render: the API](#render-the-api)
- [Cloudflare Pages: the front end](#cloudflare-pages-the-front-end)
- [The GitHub App trap](#the-github-app-trap)
- [Known costs](#known-costs)

## Order of operations

Database, then API, then front end. Each needs the previous one's address.

1. Create the Neon project; copy the connection string into Render, never into the repository.
2. Deploy the API; confirm `/health/ready` returns 200 and `/health/version` reports your commit.
3. Deploy the front end with the API address written in at deploy time.
4. Add the front end's origin to the API's CORS list.

## Neon: the database

Create a project in the region nearest your users. Take the **pooled** connection string and
keep `sslmode=require`.

It goes into Render's environment, nowhere else. A developer machine does not need production
credentials, and not having them removes a category of accident entirely. If a connection
string ever lands in a chat, a log or a commit, rotate it — assume it was captured.

Neon scales to zero and wakes in under a second, so its cold start is not worth mentioning to
users.

Two connection details matter. The pooled endpoint sits behind PgBouncer, which does not
support prepared statements in transaction mode — Npgsql needs `No Reset On Close=true` and
`Max Auto Prepare=0`, or you get intermittent errors that look like corruption. Run migrations
against the **direct** (non-pooled) host instead, since they want a real session. Free tiers
also cap connections low, so set a modest `Maximum Pool Size` rather than leaving the default.

For one-off queries later, the Neon console has a SQL editor. Run a `SELECT` before any
`DELETE`, and wrap destructive work in `BEGIN; ... ROLLBACK;` first so you can read what it
would have done.

## Render: the API

Deploy from `assets/render.yaml` as a Blueprint. The parts that matter:

```yaml
healthCheckPath: /health/live     # never touches the database
buildFilter:
  paths:                          # only rebuild when the API actually changed
    - src/MyApp.Api/**
    - src/MyApp.Application/**
    - src/MyApp.Domain/**
    - src/MyApp.Infrastructure/**
    - src/MyApp.Shared/**
    - render.yaml
envVars:
  - key: ConnectionStrings__Default
    sync: false                   # set in the dashboard, never in the repo
```

Three details worth knowing:

**Honour `PORT`.** Render assigns one; bind to it or the container is unreachable.

```csharp
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://*:{port}");
}
```

**Workstation GC on a 512 MB instance.** ASP.NET Core enables Server GC by default, which keeps
a heap per core and is tuned for machines with memory to spare. Set `DOTNET_gcServer=0`.

**Terminate TLS outside, and tell ASP.NET Core about it.** The platform terminates HTTPS and
forwards plain HTTP, so `UseHttpsRedirection()` sees an insecure request and redirects — and
the client comes back through the same proxy, forever. Add `UseForwardedHeaders` as the first
middleware, before anything that inspects the scheme:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

**Environment variables use a double underscore for nesting.** `ConnectionStrings__Default`
maps to `ConnectionStrings:Default`. A single underscore silently configures nothing, and the
app starts and fails on first query.

**Do not add a container `HEALTHCHECK` that needs a package.** Installing `curl` in the runtime
image to support one makes every build depend on a Debian mirror, and nothing consumes it —
Render probes `healthCheckPath` from outside the container. It is pure downside.

## Cloudflare Pages: the front end

Cloudflare's build system has no .NET SDK, so build on a GitHub runner and hand Cloudflare the
finished static files. `assets/workflows/cloudflare-pages.yml` does this: publish, write the
API address, re-stamp integrity hashes, check the routing files survived, deploy, then verify
the live site serves what was intended.

Two files in `wwwroot` do the routing and caching, and Cloudflare consumes them as
configuration rather than serving them:

- `_redirects` — `/*  /index.html  200` so deep links reach the SPA router
- `_headers` — the cache rules from `blazor-pwa.md`

Repository secrets needed: `CLOUDFLARE_API_TOKEN` (with Pages: Edit) and
`CLOUDFLARE_ACCOUNT_ID`. The API address belongs in a repository *variable*, not a secret — it
is not sensitive and you will want to read it.

Pick the Pages project name deliberately: it becomes `name.pages.dev`, and it is the URL people
will see.

## The GitHub App trap

Worth its own section, because it cost an afternoon and produced no error anywhere.

Connecting a host like Render to GitHub involves two separate things:

- **Authorized** — the app may act on your behalf. Enough to sign you in and show a dashboard.
- **Installed** — the app has access to repositories, and receives push webhooks.

A host can be authorised but not installed. Everything looks healthy: the dashboard loads, the
service runs, past deploys are listed. Pushes simply produce **no event at all** — not a failed
build, nothing.

Check both under GitHub → Settings → Applications. "Installed GitHub Apps" is the tab that
matters; a host missing there cannot hear your pushes.

**Renaming a repository can break this**, and the asymmetry is instructive: GitHub Actions
workflows live *inside* the repository and keep working, so a front end deployed that way is
unaffected, while anything reaching in from outside may lose access silently. After renaming a
repository, push a trivial backend change and confirm it deploys. A project Pages site also does
not follow the rename — it needs a deploy to appear at the new address.

Prefer granting access to **all repositories** when installing, so the next rename does not
repeat this.

## Known costs

**Render free instances sleep after 15 idle minutes** and take roughly a minute to wake, so the
first visit after a quiet spell sits on a loading spinner. Tell users; it reads as a broken app
otherwise.

**Migrating on startup means a single replica.** Fine at this size, and worth writing down
before someone scales out and two instances race the same migration.
