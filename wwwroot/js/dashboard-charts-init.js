// ==========================================
// Dashboard Graphs Initialization
// MCBusiness - Tous les Dashboards
// ==========================================

// Helper: Initialiser un graphique ligne
function initLineChart(canvasId, labels, dataValues, label, color) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    
    return new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: label,
                data: dataValues,
                borderColor: color || '#007bff',
                backgroundColor: (color || '#007bff') + '22', // Transparence
                tension: 0.4,
                fill: true,
                pointRadius: 5,
                pointBackgroundColor: color || '#007bff'
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                }
            },
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });
}

// Helper: Initialiser un graphique secteurs
function initPieChart(canvasId, labels, dataValues, title) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    
    return new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: dataValues,
                backgroundColor: [
                    '#28a745', // Vert (actif)
                    '#ffc107', // Jaune (inactif)
                    '#dc3545'  // Rouge (annulé)
                ],
                borderColor: '#fff',
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    position: 'bottom'
                },
                title: {
                    display: true,
                    text: title
                }
            }
        }
    });
}

// ==========================================
// ADMIN DASHBOARD
// ==========================================
function initAdminCharts() {
    // Graphique revenus mensuels
    const revenueCanvas = document.getElementById('analyticsChart');
    if (revenueCanvas) {
        initLineChart(
            'analyticsChart',
            window.adminRevenueLabels || [],
            window.adminRevenueValues || [],
            'Revenus HTG',
            '#28a745'
        );
    }

    // Graphique répartition abonnements
    const subscriptionCanvas = document.getElementById('subscriptionsChart');
    if (subscriptionCanvas) {
        initPieChart(
            'subscriptionsChart',
            ['Actifs', 'Inactifs', 'Annulés'],
            [
                window.activeSubscriptions || 0,
                window.inactiveSubscriptions || 0,
                window.cancelledSubscriptions || 0
            ],
            'Répartition Abonnements'
        );
    }
}

// ==========================================
// SUPPLIER DASHBOARD
// ==========================================
function initSupplierCharts() {
    // Graphique performance commerciale
    const businessCanvas = document.getElementById('businessChart');
    if (businessCanvas) {
        initLineChart(
            'businessChart',
            window.supplierSalesLabels || [],
            window.supplierSalesValues || [],
            'Chiffre d\'affaires HTG',
            '#007bff'
        );
    }

    // Graphique répartition par catégorie
    const categoryCanvas = document.getElementById('categoryChart');
    if (categoryCanvas) {
        const categories = window.supplierCategoryLabels || [];
        const values = window.supplierCategoryValues || [];
        
        initPieChart(
            'categoryChart',
            categories,
            values,
            'Répartition par Catégorie'
        );
    }
}

// ==========================================
// USER DASHBOARD (AFFILIÉ)
// ==========================================
function initUserCharts() {
    // Graphique commissions mensuels
    const commissionsCanvas = document.getElementById('commissionsChart');
    if (commissionsCanvas) {
        initLineChart(
            'commissionsChart',
            window.userCommissionLabels || [],
            window.userCommissionValues || [],
            'Commissions HTG',
            '#ffc107'
        );
    }

    // Graphique parrainages
    const referralsCanvas = document.getElementById('referralsChart');
    if (referralsCanvas) {
        initLineChart(
            'referralsChart',
            window.userReferralLabels || [],
            window.userReferralValues || [],
            'Parrainages',
            '#28a745'
        );
    }
}

// ==========================================
// EMPLOYEE DASHBOARD
// ==========================================
function initEmployeeCharts() {
    // Graphique performance
    const performanceCanvas = document.getElementById('performanceChart');
    if (performanceCanvas) {
        initLineChart(
            'performanceChart',
            window.employeePerformanceLabels || [],
            window.employeePerformanceValues || [],
            'Score Performance %',
            '#007bff'
        );
    }

    // Graphique distribution tâches
    const tasksCanvas = document.getElementById('tasksChart');
    if (tasksCanvas) {
        const taskLabels = window.employeeTaskLabels || ['Complétées', 'En cours', 'En attente'];
        const taskValues = window.employeeTaskValues || [0, 0, 0];
        
        initPieChart(
            'tasksChart',
            taskLabels,
            taskValues,
            'Distribution Tâches'
        );
    }
}

// ==========================================
// APPELS DEPUIS LES VUES RAZOR
// ==========================================

/*
ADMIN DASHBOARD (Dashboard.cshtml):
-------------------------------------
<script>
    // Données du serveur
    window.adminRevenueLabels = @Json.Serialize(Model.DashboardData.MonthlyRevenueLabels);
    window.adminRevenueValues = @Json.Serialize(Model.DashboardData.MonthlyRevenueValues);
    window.activeSubscriptions = @Model.DashboardData.ActiveSubscriptions;
    window.inactiveSubscriptions = @Model.DashboardData.InactiveSubscriptions;
    window.cancelledSubscriptions = @Model.DashboardData.CancelledSubscriptions;
    
    // Initialisation
    document.addEventListener('DOMContentLoaded', initAdminCharts);
</script>


SUPPLIER DASHBOARD (Dashboard.cshtml):
-------------------------------------
<script>
    // Données du serveur
    window.supplierSalesLabels = @Json.Serialize(Model.SalesLabels);
    window.supplierSalesValues = @Json.Serialize(Model.SalesValues);
    window.supplierCategoryLabels = @Json.Serialize(Model.DistLabels);
    window.supplierCategoryValues = @Json.Serialize(Model.DistValues);
    
    // Initialisation
    document.addEventListener('DOMContentLoaded', initSupplierCharts);
</script>


USER DASHBOARD (Dashboard.cshtml):
---------------------------------
<script>
    // Données du serveur
    window.userCommissionLabels = @Json.Serialize(Model.Labels);
    window.userCommissionValues = @Json.Serialize(Model.ValuesGain);
    window.userReferralLabels = @Json.Serialize(Model.LabelsGain);
    window.userReferralValues = @Json.Serialize(Model.Values);
    
    // Initialisation
    document.addEventListener('DOMContentLoaded', initUserCharts);
</script>


EMPLOYEE DASHBOARD (Dashboard.cshtml):
--------------------------------------
<script>
    // Données du serveur
    window.employeePerformanceLabels = @Json.Serialize(Model.PerformanceLabels);
    window.employeePerformanceValues = @Json.Serialize(Model.PerformanceValues);
    window.employeeTaskLabels = @Json.Serialize(Model.TaskDistributionLabels);
    window.employeeTaskValues = @Json.Serialize(Model.TaskDistributionValues);
    
    // Initialisation
    document.addEventListener('DOMContentLoaded', initEmployeeCharts);
</script>
*/

// ==========================================
// FONCTIONS UTILITAIRES
// ==========================================

function refreshDashboard() {
    location.reload();
}

function toggleChart(type) {
    console.log('Switching chart view to:', type);
    location.reload(); // Simplification - adapter selon vos besoins
}

function toggleCommissionChart(type) {
    console.log('Toggling commission chart to:', type);
    location.reload(); // Simplification - adapter selon vos besoins
}

function clockInOut() {
    if (confirm('Pointer maintenant?')) {
        // Appel API à implémenter
        console.log('Clock in/out requested');
    }
}

// ==========================================
// INITIALISATION GLOBALE
// ==========================================

document.addEventListener('DOMContentLoaded', function() {
    // Déterminer le type de dashboard et initialiser les graphiques appropriés
    const dashboardType = document.body.getAttribute('data-dashboard-type');
    
    switch(dashboardType) {
        case 'admin':
            initAdminCharts();
            break;
        case 'supplier':
            initSupplierCharts();
            break;
        case 'user':
            initUserCharts();
            break;
        case 'employee':
            initEmployeeCharts();
            break;
    }
});
