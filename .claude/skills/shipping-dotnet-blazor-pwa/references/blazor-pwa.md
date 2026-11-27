# Blazor WASM PWA

Everything here is a trap that looks fine until a user is stuck on an old build.

## Contents

- [The service worker overrides your cache headers](#the-service-worker-overrides-your-cache-headers)
- [Runtime configuration](#runtime-configuration)
- [Integrity hashes](#integrity-hashes)
- [Cache headers](#cache-headers)
- [Root domain vs project subpath](#root-domain-vs-project-subpath)
- [Checking it actually works](#checking-it-actually-works)

## The service worker overrides your cache headers

Blazor's published service worker is **cache-first for every GET**, and the publish output
precaches `index.html`, `appsettings.json` and `css/app.css`.

A service worker answers *before* the network is consulted. So every `no-store` rule you set on
your CDN is correct, deployed, and completely unreachable for anyone who has visited the site
before. They keep running an old build, pointed at whichever API address was current on their
first visit, and nothing you deploy can reach them.

This is the single most expensive thing in this document, because the symptoms look like
anything but caching: the site "is down" for one person while `curl` returns 200 and incognito
works perfectly. Both of those bypass the worker, not the CDN.

The stock worker has two further problems:

- `cache.addAll` rejects the entire install if any one asset fails its integrity check, so the
  new worker never activates and the old one keeps serving its cache indefinitely.
- It calls neither `skipWaiting` nor `clients.claim`, so even a successful install waits until
  every tab of the site is closed — which, on a phone, may be never.

**Use `assets/service-worker.published.js`.** It is network-first with the cache as an offline
fallback, caches assets individually, and takes over immediately. Speed is unaffected: `fetch`
inside the worker still honours `Cache-Control`, and `_framework/*` is immutable, so those
files never touch the network either way. What changes is that the files deciding *which build*
and *which API* you get are always fresh.

One subtlety it handles: some hosts answer `/index.html` with a 308 to `/`, and the Cache API
refuses to serve a response carrying the redirected flag to a navigation request. The worker
copies the body into a fresh response to drop that flag, or the offline fallback throws instead
of showing the shell.

**The blank screen has the same root.** Mobile browsers evict Cache Storage *partially* under
storage pressure without unregistering the worker. A cached old `index.html` then asks for
`_framework/` files by fingerprints that no longer exist in the current deployment, they 404,
and Blazor dies before rendering anything — a white page with no error. The intermittency is
the signal, not noise.

**Guard the registration.** `navigator.serviceWorker` is undefined in iOS private browsing and
throws on access, taking the rest of your startup script with it:

```html
<script>
  if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
  }
</script>
```

`updateViaCache: 'none'` matters as much as the feature check: if the worker script itself
comes from the HTTP cache, the browser's byte-comparison sees no change and your fix can never
arrive. That failure is self-sealing.

**If a user is already stuck**, nothing you deploy helps until their worker releases. They must
close every tab of the site — not reload, close — and reopen. Incognito working is the
confirmation, not a workaround.

## Runtime configuration

Blazor WASM runs in the browser, so anything baked in at publish time is fixed for the life of
that build. Do not compile the API address in.

Publish once, then write the address into `wwwroot/appsettings.json` at deploy time:

```bash
printf '{\n  "ApiBaseUrl": "%s"\n}\n' "${API_BASE_URL}" > publish/wwwroot/appsettings.json
```

Fail the build loudly if the variable is unset. A missing API address produces a site that
loads, looks entirely correct, and silently cannot sign anyone in — the worst failure mode
available.

## Integrity hashes

`service-worker-assets.js` records a SHA-256 for every precached file. If your pipeline rewrites
anything after `dotnet publish` — and the step above does exactly that — those hashes are now
wrong. The integrity check fails and the app quietly stops being installable as a PWA while
continuing to work, so nothing visibly breaks.

Run `scripts/fix_service_worker_integrity.py` after any post-publish edit. Exclude files the
host consumes as configuration rather than serving (`_redirects`, `_headers` on Cloudflare) —
they are not fetched, so a hash for them can never match.

Verifying is worthwhile and cheap:

```bash
LIVE=$(curl -sL "$SITE/appsettings.json" | openssl dgst -sha256 -binary | openssl base64)
MANIFEST=$(curl -s "$SITE/service-worker-assets.js" | tr '{' '\n' \
  | grep '"appsettings.json"' | sed -E 's/.*"sha256-([^"]+)".*/\1/')
[ "$LIVE" = "$MANIFEST" ] && echo "integrity OK" || echo "MISMATCH"
```

## Cache headers

Split by whether the filename changes when the content does.

| Path | Rule | Why |
|---|---|---|
| `/` **and** `/index.html` | `no-store` | Decides which build you get. The rule must name `/`:
visitors request the root or a deep link, never `/index.html`, and the host matches on the
requested path |
| `/appsettings.json` | `no-store` | Decides which API you talk to |
| `/service-worker.js` | `no-store` | Decides whether you get a new worker at all |
| `/_framework/*` | `immutable`, 1 year | Content-hashed: name changes with content |
| `/css/*`, `/js/*` | `no-cache` | Not fingerprinted; a cached copy hides every edit |

`no-cache` still allows the browser to keep the file — it just must revalidate — so it is the
right setting for hand-written CSS and JS, not `no-store`.

`assets/_headers` is the Cloudflare Pages version. Note that some hosts ignore you entirely:
GitHub Pages forces `max-age=600` on everything, which means a deploy that changes the API
address leaves browsers calling the old one for ten minutes. That is a reason to prefer a host
that honours the rules.

## Root domain vs project subpath

A project site served from `user.github.io/repo/` needs three workarounds: a `<base href>`
rewrite after publish, a `.nojekyll` file (Jekyll silently drops `_framework`), and a
`404.html` copy of the shell so deep links reach the SPA router.

A root domain needs none of them. If you have the choice, take the root domain.

If you must use a subpath, derive it rather than writing it down:

```bash
BASE="/${GITHUB_REPOSITORY#*/}/"
sed -i "s|<base href=\"/\" />|<base href=\"${BASE}\" />|g" publish/wwwroot/index.html
```

Then a repository rename moves the site without a code change. It still needs a deploy to run,
because a project site does not follow the redirect the host leaves behind.

## Checking it actually works

A 200 proves the host answered, not that it answered with your build. Check:

```bash
curl -s "$SITE/" | grep -o '<base href="[^"]*"'       # right subpath
curl -s "$SITE/appsettings.json"                       # right API
curl -sI "$SITE/index.html" | grep -i cache-control    # rules applied
curl -s "$SITE/service-worker.js" | head -1            # new manifest version
```

For installability, the browser's own tooling is the only real answer: DevTools → Application →
Service Workers (one active worker, no errors) and → Manifest. Automated browsers frequently
block service workers, so "I could not verify installability" is an honest result worth
reporting rather than glossing.
