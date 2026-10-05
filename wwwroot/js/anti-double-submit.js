/**
 * MCBusiness - Anti-Double Submit Protection
 * Protection globale contre les doubles soumissions de formulaires
 * Version 1.0
 */

(function() {
    'use strict';

    // Map pour tracker les formulaires en cours de soumission
    const submittingForms = new WeakMap();

    /**
     * Protection automatique pour tous les formulaires critiques
     * Applique la protection sur tous les formulaires avec l'attribut data-critical="true"
     */
    function initializeAutoProtection() {
        document.addEventListener('DOMContentLoaded', function() {
            // Protéger tous les formulaires critiques
            const criticalForms = document.querySelectorAll('form[data-critical="true"]');
            criticalForms.forEach(form => {
                protectForm(form);
            });

            // Protéger tous les boutons critiques individuels
            const criticalButtons = document.querySelectorAll('button[data-critical="true"], input[type="submit"][data-critical="true"]');
            criticalButtons.forEach(button => {
                protectButton(button);
            });
        });
    }

    /**
     * Protège un formulaire contre la double soumission
     * @param {HTMLFormElement} form - Le formulaire à protéger
     * @param {Object} options - Options de configuration
     */
    function protectForm(form, options = {}) {
        if (!form || form.tagName !== 'FORM') {
            console.error('protectForm: Invalid form element');
            return;
        }

        const config = {
            disableOnSubmit: true,
            showSpinner: true,
            buttonText: 'Traitement en cours...',
            spinnerClass: 'spinner-border spinner-border-sm',
            ...options
        };

        // Éviter la double initialisation
        if (form.dataset.antiDoubleSubmitInitialized === 'true') {
            return;
        }
        form.dataset.antiDoubleSubmitInitialized = 'true';

        form.addEventListener('submit', function(e) {
            // Vérifier si déjà en cours de soumission
            if (submittingForms.get(form) === true) {
                e.preventDefault();
                e.stopImmediatePropagation();
                console.warn('Form already submitting, blocked duplicate submission');
                return false;
            }

            // Marquer comme en cours de soumission
            submittingForms.set(form, true);

            // Désactiver tous les boutons de soumission
            if (config.disableOnSubmit) {
                const submitButtons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
                submitButtons.forEach(btn => {
                    btn.disabled = true;
                    btn.dataset.originalText = btn.textContent || btn.value;

                    if (config.showSpinner) {
                        if (btn.tagName === 'BUTTON') {
                            // Ajouter un spinner
                            const spinner = document.createElement('span');
                            spinner.className = config.spinnerClass;
                            spinner.setAttribute('role', 'status');
                            spinner.setAttribute('aria-hidden', 'true');
                            spinner.dataset.antiDoubleSubmitSpinner = 'true';
                            
                            btn.textContent = config.buttonText + ' ';
                            btn.appendChild(spinner);
                        } else {
                            btn.value = config.buttonText;
                        }
                    }
                });
            }

            // Réinitialiser après un délai de sécurité (en cas d'erreur de validation côté client)
            setTimeout(() => {
                if (form.checkValidity && !form.checkValidity()) {
                    resetForm(form);
                }
            }, 100);
        });

        // Réinitialiser en cas de problème (navigation back, erreur, etc.)
        window.addEventListener('pageshow', function(event) {
            if (event.persisted) {
                resetForm(form);
            }
        });
    }

    /**
     * Protège un bouton individuel contre le double-clic
     * @param {HTMLButtonElement} button - Le bouton à protéger
     * @param {Object} options - Options de configuration
     */
    function protectButton(button, options = {}) {
        if (!button) {
            console.error('protectButton: Invalid button element');
            return;
        }

        const config = {
            timeout: 2000, // Temps en ms avant de réactiver le bouton
            showSpinner: true,
            buttonText: 'Traitement...',
            ...options
        };

        // Éviter la double initialisation
        if (button.dataset.antiDoubleClickInitialized === 'true') {
            return;
        }
        button.dataset.antiDoubleClickInitialized = 'true';

        button.addEventListener('click', function(e) {
            if (button.disabled) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return false;
            }

            // Désactiver le bouton
            button.disabled = true;
            const originalText = button.textContent || button.value;
            button.dataset.originalText = originalText;

            if (config.showSpinner && button.tagName === 'BUTTON') {
                const spinner = document.createElement('span');
                spinner.className = 'spinner-border spinner-border-sm me-1';
                spinner.setAttribute('role', 'status');
                spinner.setAttribute('aria-hidden', 'true');
                spinner.dataset.antiDoubleClickSpinner = 'true';
                
                button.textContent = config.buttonText + ' ';
                button.appendChild(spinner);
            }

            // Réactiver après timeout (sécurité)
            setTimeout(() => {
                resetButton(button);
            }, config.timeout);
        });
    }

    /**
     * Réinitialise un formulaire à son état initial
     * @param {HTMLFormElement} form - Le formulaire à réinitialiser
     */
    function resetForm(form) {
        submittingForms.set(form, false);

        const submitButtons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
        submitButtons.forEach(btn => {
            btn.disabled = false;
            
            // Restaurer le texte original
            if (btn.dataset.originalText) {
                if (btn.tagName === 'BUTTON') {
                    // Supprimer le spinner
                    const spinner = btn.querySelector('[data-anti-double-submit-spinner]');
                    if (spinner) {
                        spinner.remove();
                    }
                    btn.textContent = btn.dataset.originalText;
                } else {
                    btn.value = btn.dataset.originalText;
                }
                delete btn.dataset.originalText;
            }
        });
    }

    /**
     * Réinitialise un bouton à son état initial
     * @param {HTMLButtonElement} button - Le bouton à réinitialiser
     */
    function resetButton(button) {
        button.disabled = false;

        if (button.dataset.originalText) {
            const spinner = button.querySelector('[data-anti-double-click-spinner]');
            if (spinner) {
                spinner.remove();
            }
            
            if (button.tagName === 'BUTTON') {
                button.textContent = button.dataset.originalText;
            } else {
                button.value = button.dataset.originalText;
            }
            delete button.dataset.originalText;
        }
    }

    /**
     * API publique
     */
    window.AntiDoubleSubmit = {
        protectForm: protectForm,
        protectButton: protectButton,
        resetForm: resetForm,
        resetButton: resetButton
    };

    // Initialiser automatiquement
    initializeAutoProtection();

})();
