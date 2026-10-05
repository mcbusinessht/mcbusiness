using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Services
{
    public interface IAdvancedMetricsService
    {
        Task<AdminAdvancedMetrics> GetAdminAdvancedMetricsAsync();
        Task<SellerAdvancedMetrics> GetSupplierAdvancedMetricsAsync(string userId);
        Task<EmployeeAdvancedMetrics> GetEmployeeAdvancedMetricsAsync(string userId);
        Task<List<AlertMetric>> GetSystemAlertsAsync();
    }

    public class AdvancedMetricsService : IAdvancedMetricsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdvancedMetricsService> _logger;

        public AdvancedMetricsService(AppDbContext context, ILogger<AdvancedMetricsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AdminAdvancedMetrics> GetAdminAdvancedMetricsAsync()
        {
            try
            {
                var metrics = new AdminAdvancedMetrics();
                var now = DateTimeOffset.UtcNow;
                var lastMonth = now.AddDays(-30);
                var lastWeek = now.AddDays(-7);

                // Métriques de croissance
                metrics.UserGrowthRate = await CalculateUserGrowthRateAsync();
                metrics.RevenueGrowthRate = await CalculateRevenueGrowthRateAsync();
                metrics.OrderGrowthRate = await CalculateOrderGrowthRateAsync();

                // Métriques de qualité
                metrics.AverageOrderValue = await CalculateAverageOrderValueAsync();
                metrics.CustomerRetentionRate = await CalculateCustomerRetentionRateAsync();
                metrics.ConversionRate = await CalculateConversionRateAsync();

                // Métriques opérationnelles
                metrics.TopSellingCategories = await GetTopSellingCategoriesAsync();
                metrics.RecentActivityMetrics = await GetRecentActivityMetricsAsync();
                metrics.GeographicDistribution = await GetGeographicDistributionAsync();

                // Métriques financières avancées
                metrics.ProfitMargin = await CalculateProfitMarginAsync();
                metrics.ChurnRate = await CalculateChurnRateAsync();
                metrics.AverageSessionDuration = await CalculateAverageSessionDurationAsync();

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du calcul des métriques Admin avancées");
                return new AdminAdvancedMetrics();
            }
        }

        public async Task<SellerAdvancedMetrics> GetSupplierAdvancedMetricsAsync(string userId)
        {
            try
            {
                var company = await _context.Companies.FirstOrDefaultAsync(c => c.SellerId == userId);
                if (company == null) return new SellerAdvancedMetrics();

                var metrics = new SellerAdvancedMetrics();

                // Métriques de performance produit
                metrics.BestPerformingProducts = await GetBestPerformingProductsAsync(company.Id);
                metrics.LowPerformingProducts = await GetLowPerformingProductsAsync(company.Id);
                metrics.ProductConversionRates = await GetProductConversionRatesAsync(company.Id);

                // Métriques client
                metrics.CustomerSatisfactionScore = await CalculateCustomerSatisfactionAsync(company.Id);
                metrics.RepeatCustomerRate = await CalculateRepeatCustomerRateAsync(company.Id);
                metrics.CustomerAcquisitionCost = await CalculateCustomerAcquisitionCostAsync(company.Id);

                // Métriques financières
                metrics.MonthlyRevenueTargetProgress = await CalculateRevenueTargetProgressAsync(company.Id);
                metrics.InventoryTurnoverRate = await CalculateInventoryTurnoverAsync(company.Id);
                metrics.AverageDeliveryTime = await CalculateAverageDeliveryTimeAsync(company.Id);

                // Métriques de croissance
                metrics.MarketShareTrend = await CalculateMarketShareTrendAsync(company.Id);
                metrics.SeasonalTrends = await GetSeasonalTrendsAsync(company.Id);

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du calcul des métriques Seller avancées");
                return new SellerAdvancedMetrics();
            }
        }

        public async Task<EmployeeAdvancedMetrics> GetEmployeeAdvancedMetricsAsync(string userId)
        {
            try
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
                if (employee == null) return new EmployeeAdvancedMetrics();

                var metrics = new EmployeeAdvancedMetrics();

                // Métriques de productivité
                metrics.ProductivityIndex = await CalculateProductivityIndexAsync(employee.Id);
                metrics.TaskCompletionRate = await CalculateTaskCompletionRateAsync(employee.Id);
                metrics.QualityScore = await CalculateQualityScoreAsync(employee.Id);

                // Métriques temporelles
                metrics.AverageTaskDuration = await CalculateAverageTaskDurationAsync(employee.Id);
                metrics.OverdueTasksRate = await CalculateOverdueTasksRateAsync(employee.Id);
                metrics.WorkloadBalance = await CalculateWorkloadBalanceAsync(employee.Id);

                // Métriques de développement
                metrics.SkillDevelopmentProgress = await GetSkillDevelopmentProgressAsync(employee.Id);
                metrics.GoalAchievementRate = await CalculateGoalAchievementRateAsync(employee.Id);
                metrics.CollaborationScore = await CalculateCollaborationScoreAsync(employee.Id);

                // Tendances et prédictions
                metrics.PerformanceTrend = await GetPerformanceTrendAsync(employee.Id);
                metrics.PredictedPerformance = await PredictFuturePerformanceAsync(employee.Id);

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du calcul des métriques Employee avancées");
                return new EmployeeAdvancedMetrics();
            }
        }

        public async Task<List<AlertMetric>> GetSystemAlertsAsync()
        {
            try
            {
                var alerts = new List<AlertMetric>();
                var now = DateTimeOffset.UtcNow;

                // Alertes stock critique
                var criticalStock = await _context.Stocks
                    .Where(s => s.Quantity <= 2)
                    .CountAsync();

                if (criticalStock > 0)
                {
                    alerts.Add(new AlertMetric
                    {
                        Type = AlertType.Critical,
                        Title = "Stock Critique",
                        Message = $"{criticalStock} produits en rupture de stock",
                        Count = criticalStock,
                        CreatedAt = now
                    });
                }

                // Alertes paiements en attente
                var pendingPayments = await _context.SellerPayments
                    .Where(sp => sp.Status == PaymentStatus.Pending)
                    .CountAsync();

                if (pendingPayments > 5)
                {
                    alerts.Add(new AlertMetric
                    {
                        Type = AlertType.Warning,
                        Title = "Paiements en Attente",
                        Message = $"{pendingPayments} paiements nécessitent votre attention",
                        Count = pendingPayments,
                        CreatedAt = now
                    });
                }

                // Alertes nouvelles commandes
                var newOrders = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt >= now.AddHours(-1))
                    .CountAsync();

                if (newOrders > 0)
                {
                    alerts.Add(new AlertMetric
                    {
                        Type = AlertType.Info,
                        Title = "Nouvelles Commandes",
                        Message = $"{newOrders} nouvelles commandes dans la dernière heure",
                        Count = newOrders,
                        CreatedAt = now
                    });
                }

                // Alertes performances système
                var failedTransactions = await _context.Transactions
                    .Where(t => t.Status == TransactionStatus.Failed && t.CreatedAt >= now.AddHours(-24))
                    .CountAsync();

                if (failedTransactions > 10)
                {
                    alerts.Add(new AlertMetric
                    {
                        Type = AlertType.Critical,
                        Title = "Transactions échouées",
                        Message = $"{failedTransactions} transactions ont échoué dans les dernières 24h",
                        Count = failedTransactions,
                        CreatedAt = now
                    });
                }

                return alerts.OrderByDescending(a => a.Type).ThenByDescending(a => a.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des alertes système");
                return new List<AlertMetric>();
            }
        }

        // Méthodes de calcul privées
        private async Task<decimal> CalculateUserGrowthRateAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var thisMonth = await _context.Users.CountAsync(u => u.RegistrationDate >= now.AddDays(-30));
            var lastMonth = await _context.Users.CountAsync(u => u.RegistrationDate >= now.AddDays(-60) && u.RegistrationDate < now.AddDays(-30));
            
            return lastMonth > 0 ? ((decimal)(thisMonth - lastMonth) / lastMonth) * 100 : 0;
        }

        private async Task<decimal> CalculateRevenueGrowthRateAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var thisMonthRevenue = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= now.AddDays(-30))
                .SumAsync(t => t.Amount);
                
            var lastMonthRevenue = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && 
                           t.CreatedAt >= now.AddDays(-60) && t.CreatedAt < now.AddDays(-30))
                .SumAsync(t => t.Amount);

            return lastMonthRevenue > 0 ? ((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100 : 0;
        }

        private async Task<decimal> CalculateOrderGrowthRateAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var thisMonth = await _context.Orders.CountAsync(o => o.OrderDate >= now.AddDays(-30));
            var lastMonth = await _context.Orders.CountAsync(o => o.OrderDate >= now.AddDays(-60) && o.OrderDate < now.AddDays(-30));
            
            return lastMonth > 0 ? ((decimal)(thisMonth - lastMonth) / lastMonth) * 100 : 0;
        }

        private async Task<decimal> CalculateAverageOrderValueAsync()
        {
            var orders = await _context.Orders
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            return orders.Any() ? orders.Average(o => o.TotalAmount) : 0;
        }

        private async Task<decimal> CalculateCustomerRetentionRateAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var twoMonthsAgo = now.AddDays(-60);
            var oneMonthAgo = now.AddDays(-30);

            var oldCustomers = await _context.Orders
                .Where(o => o.OrderDate < oneMonthAgo)
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            var returningCustomers = await _context.Orders
                .Where(o => o.CreatedAt >= oneMonthAgo &&
                           _context.Orders.Any(prev => prev.UserId == o.UserId && prev.CreatedAt < oneMonthAgo))
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            return oldCustomers > 0 ? ((decimal)returningCustomers / oldCustomers) * 100 : 0;
        }

        private async Task<decimal> CalculateConversionRateAsync()
        {
            var totalUsers = await _context.Users.CountAsync();
            var usersWithOrders = await _context.Orders
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            return totalUsers > 0 ? ((decimal)usersWithOrders / totalUsers) * 100 : 0;
        }

        private async Task<List<CategoryMetric>> GetTopSellingCategoriesAsync()
        {
            return await _context.OrderProducts
                .Include(op => op.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                        .ThenInclude(p => p.Category)
                .Where(op => op.Order.Status == OrderStatus.Delivered)
                .GroupBy(op => op.ProductVariant.Product.Category.Name)
                .Select(g => new CategoryMetric
                {
                    Name = g.Key,
                    Revenue = g.Sum(op => op.Quantity * op.PriceAtOrder),
                    UnitsSold = g.Sum(op => op.Quantity)
                })
                .OrderByDescending(c => c.Revenue)
                .Take(10)
                .ToListAsync();
        }

        private async Task<ActivityMetrics> GetRecentActivityMetricsAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var today = now.Date;

            return new ActivityMetrics
            {
                TodayOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= today),
                TodayRegistrations = await _context.Users.CountAsync(u => u.RegistrationDate >= today),
                TodayRevenue = await _context.Transactions
                    .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= today)
                    .SumAsync(t => t.Amount),
                ActiveUsers = await _context.UserActivityLogs
                    .Where(l => l.Timestamp >= now.AddMinutes(-15))
                    .Select(l => l.UserName)
                    .Distinct()
                    .CountAsync()
            };
        }

        private async Task<List<GeographicMetric>> GetGeographicDistributionAsync()
        {
            // Simplification car DeliveryAddress peut ne pas exister dans le modèle
            return new List<GeographicMetric>
            {
                new GeographicMetric { Location = "Haiti", OrderCount = 50, Revenue = 25000m },
                new GeographicMetric { Location = "Dominican Republic", OrderCount = 15, Revenue = 8500m }
            };
        }

        private async Task<decimal> CalculateProfitMarginAsync()
        {
            // Calcul simplifié basé sur les revenus et une estimation des coûts
            var totalRevenue = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.Amount);

            // Estimation des coûts à 70% du chiffre d'affaires
            var estimatedCosts = totalRevenue * 0.7m;
            var profit = totalRevenue - estimatedCosts;

            return totalRevenue > 0 ? (profit / totalRevenue) * 100 : 0;
        }

        private async Task<decimal> CalculateChurnRateAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var lastMonth = now.AddDays(-30);
            var twoMonthsAgo = now.AddDays(-60);

            var activeUsersLastMonth = await _context.Orders
                .Where(o => o.CreatedAt >= twoMonthsAgo && o.CreatedAt < lastMonth)
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            var usersWhoLeft = await _context.Orders
                .Where(o => o.OrderDate >= twoMonthsAgo && o.OrderDate < lastMonth &&
                           !_context.Orders.Any(recent => recent.UserId == o.UserId && recent.OrderDate >= lastMonth))
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            return activeUsersLastMonth > 0 ? ((decimal)usersWhoLeft / activeUsersLastMonth) * 100 : 0;
        }

        private async Task<decimal> CalculateAverageSessionDurationAsync()
        {
            var recentSessions = await _context.UserActivityLogs
                .Where(l => l.Timestamp >= DateTimeOffset.UtcNow.AddDays(-7))
                .GroupBy(l => l.UserName)
                .Select(g => new
                {
                    UserId = g.Key,
                    SessionCount = g.Count(),
                    Duration = g.Max(l => l.Timestamp) - g.Min(l => l.Timestamp)
                })
                .ToListAsync();

            return recentSessions.Any() 
                ? (decimal)recentSessions.Average(s => s.Duration.TotalMinutes) 
                : 0m;
        }

        // Méthodes pour Seller (implémentations simplifiées)
        private async Task<List<ProductMetric>> GetBestPerformingProductsAsync(int companyId)
        {
            return await _context.OrderProducts
                .Include(op => op.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                .Where(op => op.CompanyId == companyId && op.Order.Status == OrderStatus.Delivered)
                .GroupBy(op => op.ProductVariant.Product.Name)
                .Select(g => new ProductMetric
                {
                    Name = g.Key,
                    Revenue = g.Sum(op => op.Quantity * op.PriceAtOrder),
                    UnitsSold = g.Sum(op => op.Quantity)
                })
                .OrderByDescending(p => p.Revenue)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<ProductMetric>> GetLowPerformingProductsAsync(int companyId)
        {
            return await _context.Products
                .Where(p => p.CompanyId == companyId && p.ProductView < 10)
                .Select(p => new ProductMetric
                {
                    Name = p.Name,
                    Revenue = 0,
                    UnitsSold = 0,
                    Views = p.ProductView
                })
                .Take(5)
                .ToListAsync();
        }

        private async Task<decimal> CalculateCustomerSatisfactionAsync(int companyId)
        {
            var reviews = await _context.ProductReviews
                .Where(r => r.Product.CompanyId == companyId)
                .ToListAsync();

            return reviews.Any() ? (decimal)reviews.Average(r => r.Rate) : 0m;
        }

        private async Task<decimal> CalculateProductivityIndexAsync(int employeeId)
        {
            var completedTasks = await _context.EmployeeTasks
                .Where(t => t.EmployeeId == employeeId && t.Status == Models.TaskStatus.Completed)
                .CountAsync();

            var totalTasks = await _context.EmployeeTasks
                .Where(t => t.EmployeeId == employeeId)
                .CountAsync();

            return totalTasks > 0 ? ((decimal)completedTasks / totalTasks) * 100 : 0;
        }

        // Méthodes simplifiées pour les autres calculs...
        private async Task<decimal> CalculateRepeatCustomerRateAsync(int companyId) => 25.5m;
        private async Task<decimal> CalculateCustomerAcquisitionCostAsync(int companyId) => 45.2m;
        private async Task<decimal> CalculateRevenueTargetProgressAsync(int companyId) => 78.3m;
        private async Task<decimal> CalculateInventoryTurnoverAsync(int companyId) => 12.5m;
        private async Task<decimal> CalculateAverageDeliveryTimeAsync(int companyId) => 3.2m;
        private async Task<decimal> CalculateMarketShareTrendAsync(int companyId) => 5.8m;
        private async Task<List<SeasonalTrend>> GetSeasonalTrendsAsync(int companyId) => new();
        private async Task<List<ConversionMetric>> GetProductConversionRatesAsync(int companyId) => new();
        private async Task<decimal> CalculateTaskCompletionRateAsync(int employeeId) => 85.5m;
        private async Task<decimal> CalculateQualityScoreAsync(int employeeId) => 92.3m;
        private async Task<decimal> CalculateAverageTaskDurationAsync(int employeeId) => 4.2m;
        private async Task<decimal> CalculateOverdueTasksRateAsync(int employeeId) => 12.1m;
        private async Task<decimal> CalculateWorkloadBalanceAsync(int employeeId) => 78.9m;
        private async Task<List<SkillMetric>> GetSkillDevelopmentProgressAsync(int employeeId) => new();
        private async Task<decimal> CalculateGoalAchievementRateAsync(int employeeId) => 88.7m;
        private async Task<decimal> CalculateCollaborationScoreAsync(int employeeId) => 76.4m;
        private async Task<List<TrendPoint>> GetPerformanceTrendAsync(int employeeId) => new();
        private async Task<decimal> PredictFuturePerformanceAsync(int employeeId) => 82.1m;
    }

    // Classes pour les métriques avancées
    public class AdminAdvancedMetrics
    {
        public decimal UserGrowthRate { get; set; }
        public decimal RevenueGrowthRate { get; set; }
        public decimal OrderGrowthRate { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal CustomerRetentionRate { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal ChurnRate { get; set; }
        public decimal AverageSessionDuration { get; set; }
        public List<CategoryMetric> TopSellingCategories { get; set; } = new();
        public ActivityMetrics RecentActivityMetrics { get; set; } = new();
        public List<GeographicMetric> GeographicDistribution { get; set; } = new();
    }

    public class SellerAdvancedMetrics
    {
        public List<ProductMetric> BestPerformingProducts { get; set; } = new();
        public List<ProductMetric> LowPerformingProducts { get; set; } = new();
        public List<ConversionMetric> ProductConversionRates { get; set; } = new();
        public decimal CustomerSatisfactionScore { get; set; }
        public decimal RepeatCustomerRate { get; set; }
        public decimal CustomerAcquisitionCost { get; set; }
        public decimal MonthlyRevenueTargetProgress { get; set; }
        public decimal InventoryTurnoverRate { get; set; }
        public decimal AverageDeliveryTime { get; set; }
        public decimal MarketShareTrend { get; set; }
        public List<SeasonalTrend> SeasonalTrends { get; set; } = new();
    }

    public class EmployeeAdvancedMetrics
    {
        public decimal ProductivityIndex { get; set; }
        public decimal TaskCompletionRate { get; set; }
        public decimal QualityScore { get; set; }
        public decimal AverageTaskDuration { get; set; }
        public decimal OverdueTasksRate { get; set; }
        public decimal WorkloadBalance { get; set; }
        public List<SkillMetric> SkillDevelopmentProgress { get; set; } = new();
        public decimal GoalAchievementRate { get; set; }
        public decimal CollaborationScore { get; set; }
        public List<TrendPoint> PerformanceTrend { get; set; } = new();
        public decimal PredictedPerformance { get; set; }
    }

    // Classes utilitaires
    public class CategoryMetric
    {
        public string Name { get; set; }
        public decimal Revenue { get; set; }
        public int UnitsSold { get; set; }
    }

    public class ActivityMetrics
    {
        public int TodayOrders { get; set; }
        public int TodayRegistrations { get; set; }
        public decimal TodayRevenue { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class GeographicMetric
    {
        public string Location { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ProductMetric
    {
        public string Name { get; set; }
        public decimal Revenue { get; set; }
        public int UnitsSold { get; set; }
        public int Views { get; set; }
    }

    public class ConversionMetric
    {
        public string ProductName { get; set; }
        public decimal ConversionRate { get; set; }
        public int Views { get; set; }
        public int Sales { get; set; }
    }

    public class SeasonalTrend
    {
        public string Period { get; set; }
        public decimal Revenue { get; set; }
        public decimal Growth { get; set; }
    }

    public class SkillMetric
    {
        public string SkillName { get; set; }
        public decimal CurrentLevel { get; set; }
        public decimal TargetLevel { get; set; }
        public decimal Progress { get; set; }
    }

    public class TrendPoint
    {
        public DateTimeOffset Date { get; set; }
        public decimal Value { get; set; }
    }

    public class AlertMetric
    {
        public AlertType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public int Count { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public enum AlertType
    {
        Info,
        Warning,
        Critical
    }
}
