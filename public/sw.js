/**
 * Minimal app-shell service worker.
 * Caches the shell (index.html, manifest, icon) so the mobile app can
 * boot offline; navigation falls back to the cached shell when the
 * network is unavailable. Static assets (JS/CSS bundle chunks) are
 * cache-first once fetched once. `/api/*` is never touched — see the
 * fetch handler below.
 */
const CACHE = 'oas-shell-v1';
const SHELL = ['/', '/index.html', '/manifest.webmanifest', '/icon.svg'];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))),
    ),
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  // Never intercept API calls — this SW caches the static app shell/bundle
  // only. A cache-first GET here would freeze every "live" screen (KPIs,
  // post-states, dashboards — all 30s-polled `/api/oas/*` GETs) at whatever
  // response was cached the first time, silently never updating again.
  if (new URL(req.url).pathname.startsWith('/api/')) return;

  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req).catch(() => caches.match('/index.html').then((r) => r || caches.match('/'))),
    );
    return;
  }

  event.respondWith(
    caches.match(req).then(
      (cached) =>
        cached ||
        fetch(req)
          .then((res) => {
            const copy = res.clone();
            caches.open(CACHE).then((c) => c.put(req, copy));
            return res;
          })
          .catch(() => cached),
    ),
  );
});
