// Atlas Control Panel — Service Worker (minimal, enables PWA install)
const CACHE_NAME = 'atlas-v1';

self.addEventListener('install', (e) => {
    self.skipWaiting();
});

self.addEventListener('activate', (e) => {
    e.waitUntil(clients.claim());
});

self.addEventListener('fetch', (e) => {
    // Network-first strategy — we're a live dashboard, always want fresh data
    e.respondWith(
        fetch(e.request).catch(() => caches.match(e.request))
    );
});
