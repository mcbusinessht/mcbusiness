// Système de mode sombre pour FriKob
class ThemeManager {
    constructor() {
        this.theme = localStorage.getItem('theme') || 'light';
        this.init();
    }

    init() {
        // Application du thème initial
        this.applyTheme(this.theme);
        
        // Création et ajout du bouton de toggle
        this.createToggleButton();
        
        // Event listeners
        this.attachEventListeners();
    }

    createToggleButton() {
        // Le bouton est maintenant créé directement dans le HTML (_Layout.cshtml)
        // Ne rien créer, juste mettre à jour l'icône du bouton existant
        const existingButton = document.getElementById('theme-toggle');
        if (existingButton) {
            this.updateButtonIcon(existingButton);
        }
        
        // NE PAS créer de nouveau bouton dans la navbar
        // L'ancien code est désactivé pour éviter les doublons
    }

    updateButtonIcon(button) {
        const icon = button.querySelector('i');
        if (icon) {
            if (this.theme === 'dark') {
                icon.className = 'bi bi-sun-fill fs-5';
            } else {
                icon.className = 'bi bi-moon-stars-fill fs-5';
            }
        }
    }

    attachEventListeners() {
        // Toggle sur clic du bouton (supporte l'ancien et le nouveau bouton)
        document.addEventListener('click', (e) => {
            if (e.target.closest('.theme-toggle') || e.target.closest('#theme-toggle')) {
                this.toggleTheme();
            }
        });

        // Raccourci clavier (Ctrl + Shift + D)
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.shiftKey && e.key === 'D') {
                e.preventDefault();
                this.toggleTheme();
            }
        });

        // Détection du thème système
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        mediaQuery.addListener(() => {
            if (!localStorage.getItem('theme')) {
                this.applyTheme(mediaQuery.matches ? 'dark' : 'light');
            }
        });
    }

    toggleTheme() {
        this.theme = this.theme === 'dark' ? 'light' : 'dark';
        this.applyTheme(this.theme);
        localStorage.setItem('theme', this.theme);
        
        // Animation de transition
        this.animateToggle();
    }

    applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        this.theme = theme;
        
        // Mettre à jour le texte du bouton (ancien système)
        const toggleText = document.querySelector('.toggle-text');
        if (toggleText) {
            toggleText.textContent = theme === 'dark' ? 'Mode Clair' : 'Mode Sombre';
        }

        // Mettre à jour l'icône du nouveau bouton
        const newButton = document.getElementById('theme-toggle');
        if (newButton) {
            this.updateButtonIcon(newButton);
        }

        // Mettre à jour la favicon en fonction du thème
        this.updateFavicon(theme);
        
        // Événement personnalisé pour les autres scripts
        window.dispatchEvent(new CustomEvent('themeChanged', { 
            detail: { theme: theme } 
        }));
    }

    animateToggle() {
        const button = document.querySelector('.theme-toggle') || document.getElementById('theme-toggle');
        if (button) {
            button.style.transform = 'scale(0.95)';
            setTimeout(() => {
                button.style.transform = 'scale(1)';
            }, 150);
        }

        // Animation de transition sur la page
        document.body.style.transition = 'background-color 0.3s ease, color 0.3s ease';
        setTimeout(() => {
            document.body.style.transition = '';
        }, 300);
    }

    updateFavicon(theme) {
        // Optionnel : changer le favicon en fonction du thème
        const favicon = document.querySelector('link[rel="icon"]');
        if (favicon && theme === 'dark') {
            // Vous pouvez avoir une version sombre de votre favicon
            // favicon.href = '/favicon-dark.ico';
        }
    }

    getCurrentTheme() {
        return this.theme;
    }

    // Méthode pour forcer un thème
    setTheme(theme) {
        if (['light', 'dark'].includes(theme)) {
            this.applyTheme(theme);
            localStorage.setItem('theme', theme);
        }
    }
}

// Gestion des graphiques Chart.js en mode sombre
function updateChartsForTheme(theme) {
    if (typeof Chart !== 'undefined') {
        Chart.defaults.color = theme === 'dark' ? '#ffffff' : '#666666';
        Chart.defaults.borderColor = theme === 'dark' ? '#404040' : '#e7e7e7';
        Chart.defaults.backgroundColor = theme === 'dark' ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)';
        
        // Redessiner tous les graphiques existants
        Object.values(Chart.instances).forEach(chart => {
            chart.options.plugins.legend.labels.color = theme === 'dark' ? '#ffffff' : '#666666';
            chart.options.scales?.x?.ticks ? (chart.options.scales.x.ticks.color = theme === 'dark' ? '#ffffff' : '#666666') : null;
            chart.options.scales?.y?.ticks ? (chart.options.scales.y.ticks.color = theme === 'dark' ? '#ffffff' : '#666666') : null;
            chart.options.scales?.x?.grid ? (chart.options.scales.x.grid.color = theme === 'dark' ? '#404040' : '#e7e7e7') : null;
            chart.options.scales?.y?.grid ? (chart.options.scales.y.grid.color = theme === 'dark' ? '#404040' : '#e7e7e7') : null;
            chart.update();
        });
    }
}

// Initialisation automatique
document.addEventListener('DOMContentLoaded', () => {
    window.themeManager = new ThemeManager();
    
    // Écouter les changements de thème pour mettre à jour les graphiques
    window.addEventListener('themeChanged', (e) => {
        updateChartsForTheme(e.detail.theme);
    });
});

// Fonction utilitaire pour les autres scripts
window.getCurrentTheme = () => {
    return window.themeManager ? window.themeManager.getCurrentTheme() : 'light';
};

window.setTheme = (theme) => {
    if (window.themeManager) {
        window.themeManager.setTheme(theme);
    }
};