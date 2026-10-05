// Service Worker for FriKob Performance Optimization
const CACHE_NAME = 'frikob-cache-v1.2';
const urlsToCache = [
  '/',
  '/css/site.bundle.min.css',
  '/js/site.bundle.min.js',
  '/js/validation.bundle.min.js',
  '/image/Logo_Frikob.png',
  '/image/frikobBLANC.png',
  '/favicon.ico',
  '/favicon.png'
];

// Cache strategies
const CACHE_STRATEGIES = {
  CACHE_FIRST: 'cache-first',
  NETWORK_FIRST: 'network-first',
  STALE_WHILE_REVALIDATE: 'stale-while-revalidate'
};

// Install event - cache essential files
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => {
        console.log('Caching essential files');
        return cache.addAll(urlsToCache);
      })
      .then(() => {
        self.skipWaiting();
      })
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheName !== CACHE_NAME) {
            console.log('Deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      self.clients.claim();
    })
  );
});

// Fetch event - implement caching strategies
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // Skip non-GET requests
  if (request.method !== 'GET') {
    return;
  }

  // Skip Chrome extension requests
  if (url.protocol === 'chrome-extension:') {
    return;
  }

  // Choose strategy based on resource type
  let strategy = getStrategyForRequest(request);
  
  event.respondWith(
    executeStrategy(request, strategy)
  );
});

function getStrategyForRequest(request) {
  const url = new URL(request.url);
  
  // Static assets - cache first
  if (url.pathname.match(/\.(css|js|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot)$/)) {
    return CACHE_STRATEGIES.CACHE_FIRST;
  }
  
  // API calls - network first
  if (url.pathname.startsWith('/api/')) {
    return CACHE_STRATEGIES.NETWORK_FIRST;
  }
  
  // HTML pages - stale while revalidate
  if (request.headers.get('accept').includes('text/html')) {
    return CACHE_STRATEGIES.STALE_WHILE_REVALIDATE;
  }
  
  // Default to network first
  return CACHE_STRATEGIES.NETWORK_FIRST;
}

function executeStrategy(request, strategy) {
  switch (strategy) {
    case CACHE_STRATEGIES.CACHE_FIRST:
      return cacheFirst(request);
    case CACHE_STRATEGIES.NETWORK_FIRST:
      return networkFirst(request);
    case CACHE_STRATEGIES.STALE_WHILE_REVALIDATE:
      return staleWhileRevalidate(request);
    default:
      return fetch(request);
  }
}

function cacheFirst(request) {
  return caches.match(request)
    .then(response => {
      if (response) {
        return response;
      }
      return fetch(request).then(fetchResponse => {
        // Don't cache non-successful responses
        if (!fetchResponse || fetchResponse.status !== 200) {
          return fetchResponse;
        }
        
        const responseToCache = fetchResponse.clone();
        caches.open(CACHE_NAME)
          .then(cache => {
            cache.put(request, responseToCache);
          });
        
        return fetchResponse;
      });
    })
    .catch(() => {
      // Return offline fallback if available
      if (request.headers.get('accept').includes('text/html')) {
        return caches.match('/offline.html');
      }
    });
}

function networkFirst(request) {
  return fetch(request)
    .then(response => {
      if (response.status === 200) {
        const responseToCache = response.clone();
        caches.open(CACHE_NAME)
          .then(cache => {
            cache.put(request, responseToCache);
          });
      }
      return response;
    })
    .catch(() => {
      return caches.match(request);
    });
}

function staleWhileRevalidate(request) {
  const fetchPromise = fetch(request)
    .then(response => {
      if (response.status === 200) {
        const responseToCache = response.clone();
        caches.open(CACHE_NAME)
          .then(cache => {
            cache.put(request, responseToCache);
          });
      }
      return response;
    });

  return caches.match(request)
    .then(cacheResponse => {
      return cacheResponse || fetchPromise;
    })
    .catch(() => {
      return fetchPromise;
    });
}

// Background sync for offline actions
self.addEventListener('sync', event => {
  if (event.tag === 'cart-sync') {
    event.waitUntil(syncCartChanges());
  }
});

function syncCartChanges() {
  return self.registration.sync.register('cart-sync')
    .then(() => {
      console.log('Cart sync registered');
    });
}