// Dashboard Charts - Scripts pour graphiques interactifs FriKob
// Utilisation de Chart.js pour des visualisations modernes

class FriKobDashboard {
    constructor() {
        this.charts = new Map();
        this.colors = {
            primary: '#005fac',
            secondary: '#3f37c9',
            success: '#198754',
            danger: '#dc3545',
            warning: '#ffc107',
            info: '#0dcaf0',
            light: '#f8f9fa',
            dark: '#212529'
        };
        this.gradients = {};
        this.init();
    }

    init() {
        // Configuration globale de Chart.js
        Chart.defaults.font.family = "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif";
        Chart.defaults.font.size = 12;
        Chart.defaults.color = this.colors.dark;
        
        // Attendre que le DOM soit chargé
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.initCharts());
        } else {
            this.initCharts();
        }
    }

    initCharts() {
        this.createGradients();
        this.initRevenueChart();
        this.initUserGrowthChart();
        this.initOrderStatusChart();
        this.initCategoryDistributionChart();
        this.initPerformanceChart();
        this.initTaskDistributionChart();
        this.initRealtimeMetrics();
    }

    createGradients() {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        
        // Gradient principal
        this.gradients.primary = ctx.createLinearGradient(0, 0, 0, 400);
        this.gradients.primary.addColorStop(0, 'rgba(31, 72, 117, 0.8)');
        this.gradients.primary.addColorStop(1, 'rgba(31, 72, 117, 0.1)');
        
        // Gradient secondaire
        this.gradients.secondary = ctx.createLinearGradient(0, 0, 0, 400);
        this.gradients.secondary.addColorStop(0, 'rgba(63, 55, 201, 0.8)');
        this.gradients.secondary.addColorStop(1, 'rgba(63, 55, 201, 0.1)');
        
        // Gradient succès
        this.gradients.success = ctx.createLinearGradient(0, 0, 0, 400);
        this.gradients.success.addColorStop(0, 'rgba(25, 135, 84, 0.8)');
        this.gradients.success.addColorStop(1, 'rgba(25, 135, 84, 0.1)');
    }

    initRevenueChart() {
        const ctx = document.getElementById('revenueChart');
        if (!ctx) return;

        // Récupérer les données depuis les éléments data
        const labels = JSON.parse(ctx.dataset.labels || '[]');
        const values = JSON.parse(ctx.dataset.values || '[]');

        this.charts.set('revenue', new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Revenus (HTG)',
                    data: values,
                    borderColor: this.colors.primary,
                    backgroundColor: this.gradients.primary,
                    borderWidth: 3,
                    fill: true,
                    tension: 0.4,
                    pointBackgroundColor: this.colors.primary,
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    pointRadius: 6,
                    pointHoverRadius: 8
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    intersect: false,
                    mode: 'index'
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        borderColor: this.colors.primary,
                        borderWidth: 1,
                        cornerRadius: 8,
                        displayColors: false,
                        callbacks: {
                            label: function(context) {
                                return `Revenus: ${context.parsed.y.toLocaleString()} HTG`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: this.colors.dark
                        }
                    },
                    y: {
                        grid: {
                            color: 'rgba(0, 0, 0, 0.1)'
                        },
                        ticks: {
                            color: this.colors.dark,
                            callback: function(value) {
                                return value.toLocaleString() + ' HTG';
                            }
                        }
                    }
                },
                animation: {
                    duration: 2000,
                    easing: 'easeInOutQuart'
                }
            }
        }));
    }

    initUserGrowthChart() {
        const ctx = document.getElementById('userGrowthChart');
        if (!ctx) return;

        const labels = JSON.parse(ctx.dataset.labels || '[]');
        const values = JSON.parse(ctx.dataset.values || '[]');

        this.charts.set('userGrowth', new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Nouveaux Utilisateurs',
                    data: values,
                    backgroundColor: this.gradients.secondary,
                    borderColor: this.colors.secondary,
                    borderWidth: 2,
                    borderRadius: 8,
                    borderSkipped: false
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        }
                    },
                    y: {
                        grid: {
                            color: 'rgba(0, 0, 0, 0.1)'
                        },
                        ticks: {
                            stepSize: 1
                        }
                    }
                },
                animation: {
                    delay: (context) => context.dataIndex * 100
                }
            }
        }));
    }

    initOrderStatusChart() {
        const ctx = document.getElementById('orderStatusChart');
        if (!ctx) return;

        const pending = parseInt(ctx.dataset.pending || '0');
        const completed = parseInt(ctx.dataset.completed || '0');
        const cancelled = parseInt(ctx.dataset.cancelled || '0');

        this.charts.set('orderStatus', new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['En attente', 'Complétées', 'Annulées'],
                datasets: [{
                    data: [pending, completed, cancelled],
                    backgroundColor: [
                        this.colors.warning,
                        this.colors.success,
                        this.colors.danger
                    ],
                    borderWidth: 0,
                    hoverBorderWidth: 3,
                    hoverBorderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            usePointStyle: true,
                            pointStyle: 'circle'
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = Math.round((context.parsed / total) * 100);
                                return `${context.label}: ${context.parsed} (${percentage}%)`;
                            }
                        }
                    }
                },
                animation: {
                    animateRotate: true,
                    duration: 2000
                }
            }
        }));
    }

    initCategoryDistributionChart() {
        const ctx = document.getElementById('categoryChart');
        if (!ctx) return;

        const labels = JSON.parse(ctx.dataset.labels || '[]');
        const values = JSON.parse(ctx.dataset.values || '[]');

        const colors = [
            this.colors.primary,
            this.colors.secondary,
            this.colors.success,
            this.colors.info,
            this.colors.warning,
            this.colors.danger
        ];

        this.charts.set('category', new Chart(ctx, {
            type: 'polarArea',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors.slice(0, labels.length).map(color => color + '80'),
                    borderColor: colors.slice(0, labels.length),
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom'
                    }
                },
                scales: {
                    r: {
                        grid: {
                            color: 'rgba(0, 0, 0, 0.1)'
                        }
                    }
                }
            }
        }));
    }

    initPerformanceChart() {
        const ctx = document.getElementById('performanceChart');
        if (!ctx) return;

        const labels = JSON.parse(ctx.dataset.labels || '[]');
        const values = JSON.parse(ctx.dataset.values || '[]');

        this.charts.set('performance', new Chart(ctx, {
            type: 'radar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Performance',
                    data: values,
                    backgroundColor: 'rgba(31, 72, 117, 0.2)',
                    borderColor: this.colors.primary,
                    borderWidth: 2,
                    pointBackgroundColor: this.colors.primary,
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    pointRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    }
                },
                scales: {
                    r: {
                        beginAtZero: true,
                        max: 100,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.1)'
                        },
                        pointLabels: {
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        }
                    }
                }
            }
        }));
    }

    initTaskDistributionChart() {
        const ctx = document.getElementById('taskChart');
        if (!ctx) return;

        const labels = JSON.parse(ctx.dataset.labels || '[]');
        const values = JSON.parse(ctx.dataset.values || '[]');

        this.charts.set('tasks', new Chart(ctx, {
            type: 'pie',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: [
                        this.colors.success,
                        this.colors.warning,
                        this.colors.info,
                        this.colors.danger
                    ],
                    borderWidth: 0,
                    hoverBorderWidth: 3,
                    hoverBorderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'right',
                        labels: {
                            padding: 20,
                            usePointStyle: true
                        }
                    }
                }
            }
        }));
    }

    initRealtimeMetrics() {
        // Mise à jour en temps réel des métriques
        this.startRealtimeUpdates();
    }

    startRealtimeUpdates() {
        // Simuler des mises à jour en temps réel
        setInterval(() => {
            this.updateActiveUsers();
            this.updateRealtimeStats();
        }, 30000); // Toutes les 30 secondes
    }

    updateActiveUsers() {
        const activeUsersElement = document.getElementById('activeUsers');
        if (activeUsersElement) {
            // Simulation d'une variation aléatoire
            const currentValue = parseInt(activeUsersElement.textContent);
            const variation = Math.floor(Math.random() * 10) - 5;
            const newValue = Math.max(0, currentValue + variation);
            
            // Animation du changement
            this.animateNumber(activeUsersElement, currentValue, newValue);
        }
    }

    updateRealtimeStats() {
        const elements = document.querySelectorAll('[data-realtime]');
        elements.forEach(element => {
            const metric = element.dataset.realtime;
            // Ici on pourrait faire un appel AJAX pour récupérer les vraies données
            this.simulateRealtimeUpdate(element, metric);
        });
    }

    simulateRealtimeUpdate(element, metric) {
        const currentValue = parseFloat(element.textContent.replace(/[^0-9.-]+/g, ''));
        let newValue;

        switch (metric) {
            case 'revenue':
                newValue = currentValue + (Math.random() * 1000);
                break;
            case 'orders':
                newValue = currentValue + Math.floor(Math.random() * 5);
                break;
            case 'users':
                newValue = currentValue + Math.floor(Math.random() * 3);
                break;
            default:
                return;
        }

        this.animateNumber(element, currentValue, newValue);
    }

    animateNumber(element, start, end) {
        const duration = 1000;
        const startTime = Date.now();
        const difference = end - start;

        const animate = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            // Easing function
            const easeOutQuart = 1 - Math.pow(1 - progress, 4);
            const current = start + (difference * easeOutQuart);
            
            // Formater selon le type de donnée
            if (element.dataset.realtime === 'revenue') {
                element.textContent = current.toLocaleString('fr-FR', {
                    style: 'currency',
                    currency: 'HTG',
                    minimumFractionDigits: 0
                }).replace('HTG', 'HTG');
            } else {
                element.textContent = Math.round(current).toLocaleString();
            }

            if (progress < 1) {
                requestAnimationFrame(animate);
            }
        };

        requestAnimationFrame(animate);
    }

    // Méthodes pour interactions avancées
    addChartFilters() {
        // Ajouter des filtres interactifs aux graphiques
        const filterButtons = document.querySelectorAll('[data-chart-filter]');
        filterButtons.forEach(button => {
            button.addEventListener('click', (e) => {
                const chartId = e.target.dataset.chartFilter;
                const period = e.target.dataset.period;
                this.updateChartData(chartId, period);
            });
        });
    }

    updateChartData(chartId, period) {
        const chart = this.charts.get(chartId);
        if (!chart) return;

        // Ici on ferait un appel AJAX pour récupérer les nouvelles données
        console.log(`Mise à jour du graphique ${chartId} pour la période ${period}`);
        
        // Simuler une mise à jour
        chart.data.datasets[0].data = chart.data.datasets[0].data.map(() => 
            Math.floor(Math.random() * 1000)
        );
        chart.update('active');
    }

    // Méthodes utilitaires
    destroyChart(chartId) {
        const chart = this.charts.get(chartId);
        if (chart) {
            chart.destroy();
            this.charts.delete(chartId);
        }
    }

    refreshChart(chartId, newData) {
        const chart = this.charts.get(chartId);
        if (chart) {
            chart.data = newData;
            chart.update();
        }
    }

    exportChart(chartId, format = 'png') {
        const chart = this.charts.get(chartId);
        if (chart) {
            const url = chart.toBase64Image();
            const link = document.createElement('a');
            link.download = `chart-${chartId}.${format}`;
            link.href = url;
            link.click();
        }
    }
}

// Initialisation automatique
window.addEventListener('DOMContentLoaded', () => {
    window.FriKobDashboard = new FriKobDashboard();
});

// Export des fonctions utilitaires
window.DashboardUtils = {
    formatCurrency: (amount, currency = 'HTG') => {
        return new Intl.NumberFormat('fr-FR', {
            style: 'currency',
            currency: currency,
            minimumFractionDigits: 0
        }).format(amount).replace(currency, currency);
    },
    
    formatNumber: (number) => {
        return new Intl.NumberFormat('fr-FR').format(number);
    },
    
    formatPercentage: (percentage, decimals = 1) => {
        return `${percentage.toFixed(decimals)}%`;
    },
    
    getGrowthIcon: (growth) => {
        if (growth > 0) return '<i class="bi bi-arrow-up text-success"></i>';
        if (growth < 0) return '<i class="bi bi-arrow-down text-danger"></i>';
        return '<i class="bi bi-dash text-muted"></i>';
    },
    
    addPulseAnimation: (element) => {
        element.classList.add('pulse-animation');
        setTimeout(() => element.classList.remove('pulse-animation'), 1000);
    }
};