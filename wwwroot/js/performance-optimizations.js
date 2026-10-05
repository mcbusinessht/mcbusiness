// Performance optimizations for FriKob

// Lazy loading for images
document.addEventListener('DOMContentLoaded', function() {
    // Image lazy loading with Intersection Observer
    if ('IntersectionObserver' in window) {
        const imageObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    img.src = img.dataset.src;
                    img.classList.remove('lazy');
                    imageObserver.unobserve(img);
                }
            });
        });

        const lazyImages = document.querySelectorAll('img[data-src]');
        lazyImages.forEach(img => {
            img.classList.add('lazy');
            imageObserver.observe(img);
        });
    }

    // Debounce for search inputs
    const searchInputs = document.querySelectorAll('input[type="search"], .search-input');
    searchInputs.forEach(input => {
        let timeout;
        input.addEventListener('input', function() {
            clearTimeout(timeout);
            timeout = setTimeout(() => {
                // Trigger search after 300ms of no typing
                const event = new Event('search-debounced');
                input.dispatchEvent(event);
            }, 300);
        });
    });

    // Optimize cart operations with batching
    let cartUpdateQueue = [];
    let cartUpDateTimeOffsetr;
    
    window.optimizedCartUpdate = function(action, data) {
        cartUpdateQueue.push({ action, data, timestamp: Date.now() });
        
        clearTimeout(cartUpDateTimeOffsetr);
        cartUpDateTimeOffsetr = setTimeout(() => {
            if (cartUpdateQueue.length > 0) {
                processBatchedCartUpdates();
            }
        }, 500);
    };

    function processBatchedCartUpdates() {
        // Group similar operations
        const uniqueOperations = cartUpdateQueue.reduce((acc, operation) => {
            const key = `${operation.action}-${operation.data.itemId || operation.data.productId}`;
            acc[key] = operation; // Keep only the latest operation for each item
            return acc;
        }, {});

        // Process each unique operation
        Object.values(uniqueOperations).forEach(operation => {
            executeCartOperation(operation);
        });

        cartUpdateQueue = [];
    }

    function executeCartOperation(operation) {
        const { action, data } = operation;
        
        switch (action) {
            case 'update-quantity':
                updateCartItemQuantity(data.itemId, data.quantity);
                break;
            case 'remove-item':
                removeCartItem(data.itemId);
                break;
            case 'add-to-cart':
                addToCart(data.productId, data.quantity);
                break;
        }
    }

    // Preload critical resources
    function preloadCriticalResources() {
        const criticalImages = [
            '/image/Logo_Frikob.png',
            '/image/frikobBLANC.png'
        ];

        criticalImages.forEach(src => {
            const link = document.createElement('link');
            link.rel = 'preload';
            link.as = 'image';
            link.href = src;
            document.head.appendChild(link);
        });
    }

    preloadCriticalResources();
});

// Service Worker registration for caching
if ('serviceWorker' in navigator && 'caches' in window) {
    window.addEventListener('load', function() {
        navigator.serviceWorker.register('/sw.js')
            .then(registration => console.log('SW registered'))
            .catch(error => console.log('SW registration failed'));
    });
}

// Performance monitoring
window.FriKobPerf = {
    measurePageLoad: function() {
        window.addEventListener('load', function() {
            setTimeout(function() {
                const perfData = performance.getEntriesByType('navigation')[0];
                console.log('Page Load Time:', perfData.loadEventEnd - perfData.fetchStart, 'ms');
                
                // Send to analytics if needed
                if (window.gtag) {
                    gtag('event', 'page_load_time', {
                        custom_parameter: Math.round(perfData.loadEventEnd - perfData.fetchStart)
                    });
                }
            }, 100);
        });
    },
    
    measureImageLoad: function(img) {
        const startTime = performance.now();
        img.addEventListener('load', function() {
            const loadTime = performance.now() - startTime;
            console.log('Image loaded in:', loadTime, 'ms');
        });
    }
};

// Initialize performance monitoring
window.FriKobPerf.measurePageLoad();