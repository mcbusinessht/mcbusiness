/**
 * Supplier Sidebar - Fonctionnalité de dropdown universelle
 * Peut être importée partout dans le projet
 */
(function(global) {
    'use strict';
    
    // Configuration par défaut
    const CONFIG = {
        selectors: {
            sidebar: '.sidebar',
            group: '.sidebar-group',
            item: '.sidebar-item',
            submenu: '.sidebar-submenu',
            subgroup: '.sidebar-subgroup',
            subitem: '.sidebar-subitem',
            toggleIcon: '.toggle-icon'
        },
        classes: {
            active: 'active',
            transitioning: 'transitioning'
        },
        animation: {
            duration: 300
        }
    };

    /**
     * Classe principale pour gérer la sidebar
     */
    class SupplierSidebar {
        constructor(options = {}) {
            this.config = Object.assign({}, CONFIG, options);
            this.initialized = false;
            this.containers = new Set();
        }

        /**
         * Initialise la sidebar dans un conteneur donné
         */
        init(container = document) {
            if (this.containers.has(container)) {
                return this; // Déjà initialisé
            }

            this.containers.add(container);
            this.setupEventListeners(container);
            this.openActiveGroups(container);
            this.initialized = true;

            return this;
        }

        /**
         * Nettoie les event listeners d'un conteneur
         */
        destroy(container = document) {
            this.containers.delete(container);
            // Les event listeners seront automatiquement supprimés avec les éléments
        }

        /**
         * Configure les event listeners pour les dropdowns
         */
        setupEventListeners(container) {
            const groups = container.querySelectorAll(this.config.selectors.group);
            
            groups.forEach(group => {
                const toggleItem = group.querySelector(':scope > ' + this.config.selectors.item);
                
                if (toggleItem && !toggleItem.hasAttribute('data-sidebar-initialized')) {
                    toggleItem.setAttribute('data-sidebar-initialized', 'true');
                    
                    // Support clavier
                    toggleItem.setAttribute('tabindex', '0');
                    toggleItem.setAttribute('role', 'button');
                    toggleItem.setAttribute('aria-expanded', group.classList.contains(this.config.classes.active));
                    
                    // Event listeners
                    toggleItem.addEventListener('click', (e) => this.handleToggle(e, group));
                    toggleItem.addEventListener('keydown', (e) => this.handleKeyboard(e, group));
                }
            });
        }

        /**
         * Gère le toggle d'un groupe
         */
        handleToggle(event, group) {
            event.preventDefault();
            event.stopPropagation();
            
            // Ne pas traiter si c'est un lien navigable
            if (event.target.closest('a[href]')) {
                return;
            }

            const toggleItem = group.querySelector(':scope > ' + this.config.selectors.item);
            const isActive = group.classList.contains(this.config.classes.active);
            
            // Accordion behavior - fermer les autres groupes
            const container = group.closest(this.config.selectors.sidebar) || document;
            this.closeOtherGroups(container, group);
            
            // Toggle le groupe actuel
            this.toggleGroup(group, !isActive);
            
            // Mettre à jour l'état ARIA
            toggleItem.setAttribute('aria-expanded', !isActive);
        }

        /**
         * Support clavier (Enter et Space)
         */
        handleKeyboard(event, group) {
            if (event.key === 'Enter' || event.key === ' ') {
                this.handleToggle(event, group);
            }
        }

        /**
         * Toggle un groupe spécifique
         */
        toggleGroup(group, show) {
            const submenu = group.querySelector(this.config.selectors.submenu);
            
            if (!submenu) return;

            // Ajouter classe de transition
            group.classList.add(this.config.classes.transitioning);
            
            if (show) {
                group.classList.add(this.config.classes.active);
                this.expandSubmenu(submenu);
            } else {
                group.classList.remove(this.config.classes.active);
                this.collapseSubmenu(submenu);
            }

            // Supprimer la classe de transition après l'animation
            setTimeout(() => {
                group.classList.remove(this.config.classes.transitioning);
            }, this.config.animation.duration);
        }

        /**
         * Étend un sous-menu
         */
        expandSubmenu(submenu) {
            // Calculer la hauteur nécessaire
            submenu.style.maxHeight = 'none';
            const height = submenu.scrollHeight;
            submenu.style.maxHeight = '0';
            
            // Forcer le reflow
            submenu.offsetHeight;
            
            // Animer vers la hauteur calculée
            submenu.style.maxHeight = height + 'px';
            
            // Permettre le contenu dynamique après l'animation
            setTimeout(() => {
                if (submenu.parentElement.classList.contains(this.config.classes.active)) {
                    submenu.style.maxHeight = 'none';
                }
            }, this.config.animation.duration);
        }

        /**
         * Réduit un sous-menu
         */
        collapseSubmenu(submenu) {
            submenu.style.maxHeight = submenu.scrollHeight + 'px';
            submenu.offsetHeight; // Force reflow
            submenu.style.maxHeight = '0';
        }

        /**
         * Ferme tous les autres groupes sauf celui spécifié
         */
        closeOtherGroups(container, currentGroup) {
            const groups = container.querySelectorAll(this.config.selectors.group);
            groups.forEach(group => {
                if (group !== currentGroup && group.classList.contains(this.config.classes.active)) {
                    this.toggleGroup(group, false);
                    
                    const toggleItem = group.querySelector(':scope > ' + this.config.selectors.item);
                    if (toggleItem) {
                        toggleItem.setAttribute('aria-expanded', 'false');
                    }
                }
            });
        }

        /**
         * Ouvre automatiquement les groupes contenant des éléments actifs
         */
        openActiveGroups(container) {
            const groups = container.querySelectorAll(this.config.selectors.group);
            
            groups.forEach(group => {
                const hasActiveChild = group.querySelector(this.config.selectors.subitem + '.active');
                if (hasActiveChild) {
                    group.classList.add(this.config.classes.active);
                    
                    const toggleItem = group.querySelector(':scope > ' + this.config.selectors.item);
                    if (toggleItem) {
                        toggleItem.setAttribute('aria-expanded', 'true');
                    }
                }
            });
        }

        /**
         * Réinitialise toutes les sidebars
         */
        reinitAll() {
            this.containers.forEach(container => {
                this.destroy(container);
                this.init(container);
            });
        }
    }

    // Instance globale
    const sidebarInstance = new SupplierSidebar();

    // API publique
    global.SupplierSidebar = {
        init: (container) => sidebarInstance.init(container),
        destroy: (container) => sidebarInstance.destroy(container),
        reinit: () => sidebarInstance.reinitAll(),
        instance: sidebarInstance
    };

    // Auto-initialisation
    function autoInit() {
        global.SupplierSidebar.init();
    }

    // DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', autoInit);
    } else {
        autoInit();
    }

    // Support Bootstrap modals et offcanvas
    document.addEventListener('shown.bs.modal', function(event) {
        const sidebar = event.target.querySelector('.sidebar');
        if (sidebar) {
            global.SupplierSidebar.init(event.target);
        }
    });

    document.addEventListener('shown.bs.offcanvas', function(event) {
        const sidebar = event.target.querySelector('.sidebar');
        if (sidebar) {
            global.SupplierSidebar.init(event.target);
        }
    });

    // Event personnalisé pour réinitialisation
    document.addEventListener('supplierSidebarReinit', function() {
        global.SupplierSidebar.reinit();
    });

})(window);