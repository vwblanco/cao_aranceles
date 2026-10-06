// Service worker de la Calculadora de Aranceles CAO.
// Precachea la aplicacion completa (incluidos Bootstrap y html2pdf.js) para funcionar sin conexion.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('message', event => {
    if (event.data === 'skipWaiting') {
        self.skipWaiting();
    }
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm$/, /\.html$/, /\.js$/, /\.mjs$/, /\.json$/, /\.webmanifest$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/, /\.blat$/, /\.dat$/ ];
// vercel.json viaja en el manifiesto de assets pero Vercel no lo sirve como
// archivo estatico: el reescrit SPA devuelve index.html y la integridad no
// coincide, lo que hace fallar cache.addAll() e impide instalar el SW.
const offlineAssetsExclude = [ /^service-worker\.js$/, /^vercel\.json$/ ];

const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
    await self.skipWaiting();
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return;
    }

const cache = await caches.open(cacheName);
  const url = new URL(event.request.url);

  const shouldServeIndexHtml = event.request.mode === 'navigate'
          && !manifestUrlList.some(url => url === event.request.url);

    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        const networkResponse = await fetch(event.request);
        if (networkResponse && networkResponse.ok && url.origin === self.origin) {
            cache.put(event.request, networkResponse.clone());
        }
        return networkResponse;
    } catch (error) {
        if (shouldServeIndexHtml) {
            const fallback = await cache.match('index.html');
            if (fallback) {
                return fallback;
            }
        }
        throw error;
    }
}
