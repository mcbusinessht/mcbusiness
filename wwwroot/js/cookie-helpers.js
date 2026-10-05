/**
 * Helpers JavaScript pour la gestion des cookies - Conforme RGPD
 * FriKob Cookie Management System
 */

class FriKobCookieManager {
    constructor() {
        this.currentSettings = null;
        this.initializeSettings();
    }

    async initializeSettings() {
        try {
            const response = await fetch('/api/cookie-consent/check', {
                credentials: 'same-origin'
            });
            const data = await response.json();
            this.currentSettings = data.settings || {};
        } catch (error) {
            console.error('Erreur lors du chargement des paramètres cookies:', error);
            this.currentSettings = { essential: true };
        }
    }

    // Vérifier si une catégorie de cookies est autorisée
    isAllowed(category) {
        if (!this.currentSettings) {
            return category === 'essential';
        }
        
        switch (category.toLowerCase()) {
            case 'essential':
                return true;
            case 'functional':
                return this.currentSettings.functional || false;
            case 'analytics':
                return this.currentSettings.analytics || false;
            case 'performance':
                return this.currentSettings.performance || false;
            case 'marketing':
                return this.currentSettings.marketing || false;
            default:
                return false;
        }
    }

    // Définir un cookie conditionnel
    setCookie(name, value, category = 'essential', options = {}) {
        if (!this.isAllowed(category)) {
            console.log(`Cookie ${name} bloqué - catégorie ${category} non autorisée`);
            return false;
        }

        const defaults = {
            path: '/',
            secure: window.location.protocol === 'https:',
            sameSite: 'Lax'
        };

        const cookieOptions = { ...defaults, ...options };
        
        let cookieString = `${name}=${encodeURIComponent(value)}`;
        
        if (cookieOptions.expires) {
            cookieString += `; expires=${cookieOptions.expires}`;
        }
        if (cookieOptions.maxAge) {
            cookieString += `; max-age=${cookieOptions.maxAge}`;
        }
        if (cookieOptions.path) {
            cookieString += `; path=${cookieOptions.path}`;
        }
        if (cookieOptions.domain) {
            cookieString += `; domain=${cookieOptions.domain}`;
        }
        if (cookieOptions.secure) {
            cookieString += `; secure`;
        }
        if (cookieOptions.sameSite) {
            cookieString += `; samesite=${cookieOptions.sameSite}`;
        }
        if (cookieOptions.httpOnly) {
            cookieString += `; httponly`;
        }

        document.cookie = cookieString;
        return true;
    }

    // Obtenir un cookie
    getCookie(name) {
        const cookies = document.cookie.split(';');
        for (let cookie of cookies) {
            const [cookieName, cookieValue] = cookie.trim().split('=');
            if (cookieName === name) {
                return decodeURIComponent(cookieValue || '');
            }
        }
        return null;
    }

    // Supprimer un cookie
    deleteCookie(name, options = {}) {
        const deleteOptions = {
            ...options,
            expires: 'Thu, 01 Jan 1970 00:00:00 GMT',
            maxAge: 0
        };
        this.setCookie(name, '', 'essential', deleteOptions);
    }

    // Initialiser Google Analytics conditionellement
    initGoogleAnalytics(trackingId) {
        if (!this.isAllowed('analytics')) {
            console.log('Google Analytics bloqué - cookies analytiques non autorisés');
            return;
        }

        // Charger Google Analytics
        const script = document.createElement('script');
        script.async = true;
        script.src = `https://www.googletagmanager.com/gtag/js?id=${trackingId}`;
        document.head.appendChild(script);

        script.onload = () => {
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', trackingId, {
                anonymize_ip: true, // Conformité RGPD
                cookie_flags: 'SameSite=Lax;Secure'
            });
            
            console.log('Google Analytics initialisé avec respect de la vie privée');
        };
    }

    // Initialiser Facebook Pixel conditionellement
    initFacebookPixel(pixelId) {
        if (!this.isAllowed('marketing')) {
            console.log('Facebook Pixel bloqué - cookies marketing non autorisés');
            return;
        }

        !function(f,b,e,v,n,t,s)
        {if(f.fbq)return;n=f.fbq=function(){n.callMethod?
        n.callMethod.apply(n,arguments):n.queue.push(arguments)};
        if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';
        n.queue=[];t=b.createElement(e);t.async=!0;
        t.src=v;s=b.getElementsByTagName(e)[0];
        s.parentNode.insertBefore(t,s)}(window, document,'script',
        'https://connect.facebook.net/en_US/fbevents.js');

        fbq('init', pixelId);
        fbq('track', 'PageView');
        
        console.log('Facebook Pixel initialisé');
    }

    // Activer le cache de performance conditionellement
    enablePerformanceCache() {
        if (!this.isAllowed('performance')) {
            console.log('Cache de performance désactivé');
            return false;
        }

        // Activer le service worker pour le cache
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('/sw-cache.js')
                .then(registration => {
                    console.log('Service Worker de cache enregistré:', registration);
                })
                .catch(error => {
                    console.error('Erreur Service Worker:', error);
                });
        }

        // Activer le localStorage pour les préférences
        this.setCookie('performance_cache_enabled', 'true', 'performance', {
            maxAge: 60 * 60 * 24 * 7 // 7 jours
        });

        return true;
    }

    // Sauvegarder les préférences utilisateur (fonctionnel)
    saveUserPreferences(preferences) {
        if (!this.isAllowed('functional')) {
            console.log('Sauvegarde des préférences bloquée');
            return false;
        }

        const prefsString = JSON.stringify(preferences);
        this.setCookie('user_preferences', prefsString, 'functional', {
            maxAge: 60 * 60 * 24 * 365 // 1 an
        });

        return true;
    }

    // Charger les préférences utilisateur
    loadUserPreferences() {
        if (!this.isAllowed('functional')) {
            return {};
        }

        const prefsString = this.getCookie('user_preferences');
        try {
            return prefsString ? JSON.parse(prefsString) : {};
        } catch (error) {
            console.error('Erreur lors du chargement des préférences:', error);
            return {};
        }
    }

    // Tracker les événements (analytics)
    trackEvent(eventName, eventData = {}) {
        if (!this.isAllowed('analytics')) {
            console.log(`Événement ${eventName} non tracké - analytics désactivés`);
            return false;
        }

        // Google Analytics 4
        if (typeof gtag !== 'undefined') {
            gtag('event', eventName, eventData);
        }

        // Facebook Pixel
        if (this.isAllowed('marketing') && typeof fbq !== 'undefined') {
            fbq('track', 'CustomEvent', { event_name: eventName, ...eventData });
        }

        console.log(`Événement tracké: ${eventName}`, eventData);
        return true;
    }

    // Afficher le gestionnaire de cookies
    showCookieManager() {
        window.location.href = '/CookieSettings';
    }

    // Révoquer tous les consentements
    async revokeAllConsents() {
        try {
            const response = await fetch('/api/cookie-consent/revoke', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                // Supprimer tous les cookies non-essentiels
                this.clearNonEssentialCookies();
                
                // Recharger la page pour appliquer les changements
                window.location.reload();
            }
        } catch (error) {
            console.error('Erreur lors de la révocation:', error);
        }
    }

    // Supprimer tous les cookies non-essentiels
    clearNonEssentialCookies() {
        const cookies = document.cookie.split(';');
        
        for (let cookie of cookies) {
            const [name] = cookie.trim().split('=');
            
            // Garder seulement les cookies essentiels
            if (!name.startsWith('.AspNetCore') && 
                name !== 'cookie_consent' && 
                name !== 'cookie_settings' && 
                !name.startsWith('__RequestVerificationToken')) {
                
                this.deleteCookie(name);
                this.deleteCookie(name, { path: '/' });
                this.deleteCookie(name, { domain: window.location.hostname });
                this.deleteCookie(name, { domain: '.' + window.location.hostname });
            }
        }

        // Vider le localStorage et sessionStorage si autorisé
        try {
            localStorage.clear();
            sessionStorage.clear();
        } catch (error) {
            console.log('Impossible de vider le stockage local');
        }
    }

    // Obtenir le statut de consentement
    async getConsentStatus() {
        try {
            const response = await fetch('/api/cookie-consent/check', {
                credentials: 'same-origin'
            });
            return await response.json();
        } catch (error) {
            console.error('Erreur lors de la récupération du statut:', error);
            return { hasConsent: false };
        }
    }
}

// Instance globale
window.FriKobCookies = new FriKobCookieManager();

// Fonctions utilitaires globales pour la compatibilité
window.cookieManager = window.FriKobCookies;

// Auto-initialisation des services selon les consentements
document.addEventListener('DOMContentLoaded', async function() {
    // Attendre que les paramètres soient chargés
    await window.FriKobCookies.initializeSettings();
    
    // Initialiser les services autorisés
    const settings = window.FriKobCookies.currentSettings;
    
    if (settings.analytics) {
        // Remplacer GA_TRACKING_ID par votre ID Google Analytics
        // window.FriKobCookies.initGoogleAnalytics('GA_TRACKING_ID');
    }
    
    if (settings.marketing) {
        // Remplacer FB_PIXEL_ID par votre ID Facebook Pixel
        // window.FriKobCookies.initFacebookPixel('FB_PIXEL_ID');
    }
    
    if (settings.performance) {
        window.FriKobCookies.enablePerformanceCache();
    }
});

// Exposer les fonctions pour l'utilisation dans d'autres scripts
window.setCookieConditional = (name, value, category, options) => 
    window.FriKobCookies.setCookie(name, value, category, options);

window.isCookieAllowed = (category) => 
    window.FriKobCookies.isAllowed(category);

window.trackEventConditional = (eventName, eventData) => 
    window.FriKobCookies.trackEvent(eventName, eventData);