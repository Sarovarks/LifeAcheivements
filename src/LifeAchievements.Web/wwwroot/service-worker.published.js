// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);

    // Deliberately not cache.addAll: that rejects the whole install if a single asset fails
    // its integrity check, so the new worker never activates and the previous one keeps
    // serving its old cache forever. Caching individually means one bad asset costs offline
    // support for that file alone, instead of wedging every visitor on a stale build.
    const results = await Promise.allSettled(
        assetsRequests.map(request => cache.add(request)));

    const failed = results.filter(r => r.status === 'rejected');
    if (failed.length > 0) {
        console.warn(`Service worker: ${failed.length} asset(s) could not be cached.`, failed[0].reason);
    }

    // Take over straight away rather than waiting for every tab of the site to be closed.
    // Without this a deploy can sit unused for days while the old worker serves stale files.
    await self.skipWaiting();
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // Control pages that were already open, so the new build is in charge immediately.
    await self.clients.claim();
}

async function onFetch(event) {
    const request = event.request;
    const url = new URL(request.url);

    // Anything that is not a plain same-origin GET - API calls, sign-ins, claims - goes
    // straight to the network and is never cached.
    if (request.method !== 'GET' || url.origin !== self.origin) {
        return fetch(request);
    }

    const isNavigation = request.mode === 'navigate';
    const fallbackKey = isNavigation ? new URL('index.html', baseUrl).href : request;

    // Network first, cache only as an offline fallback.
    //
    // This used to be cache-first, which quietly defeated every cache rule in _headers:
    // a service worker answers before the network is consulted, so marking index.html and
    // appsettings.json no-store on Cloudflare did nothing for anyone who had already
    // visited once. They kept running an old build, pointed at whichever API address was
    // current when they first loaded the site.
    //
    // Network-first costs nothing in speed, because the real caching still happens in the
    // HTTP cache: fetch() here honours Cache-Control, and _framework/* is immutable, so
    // those files are served locally without a network round trip anyway. What changes is
    // that the files which decide WHICH build and WHICH API you get are now always fresh.
    try {
        const response = await fetch(request);
        if (response && response.ok) {
            const copy = response.clone();
            event.waitUntil(
                caches.open(cacheName)
                    .then(cache => cache.put(fallbackKey, copy))
                    .catch(() => { /* an uncacheable response is not worth failing over */ }));
        }
        return response;
    } catch (error) {
        // Offline. Serve the last good copy; a deep link falls back to index.html so the
        // app shell still starts and can say what is wrong.
        const cache = await caches.open(cacheName);
        const cached = await cache.match(fallbackKey);
        return cached || Response.error();
    }
}
