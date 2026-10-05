using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Services
{
    public interface IDashboardService
    {
        Task<AdminDashboardData> GetAdminDashboardDataAsync();
        Task<SellerDashboardData> GetSellerDashboardDataAsync(string userId);
        Task<EmployeeDashboardData> GetEmployeeDashboardDataAsync(string userId);
        Task<UserDashboardData> GetUserDashboardDataAsync(string userId);
    }

    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(AppDbContext context, ILogger<DashboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AdminDashboardData> GetAdminDashboardDataAsync()
        {
            try
            {
                var data = new AdminDashboardData();

                // Statistiques utilisateurs
                data.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
                data.NewUsersThisMonth = await _context.Users
                    .CountAsync(u => !u.IsDeleted && u.RegistrationDate >= DateTimeOffset.UtcNow.AddDays(-30));

                // Statistiques abonnements
                data.ActiveSubscriptions = await _context.AmbassadorSubscriptions
                    .CountAsync(s => s.Status == SubscriptionStatus.Active);
                data.InactiveSubscriptions = await _context.AmbassadorSubscriptions
                    .CountAsync(s => s.Status == SubscriptionStatus.Inactive);
                data.CancelledSubscriptions = await _context.AmbassadorSubscriptions
                    .CountAsync(s => s.Status == SubscriptionStatus.Cancelled);
                data.TotalPlans = await _context.AmbassadorPlans
                    .CountAsync(p => !p.IsDeleted && p.IsActive);

                var platformTransactions = _context.PlatformTransactions
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted);

                // Statistiques transactions comptables plateforme.
                data.TotalTransactions = await platformTransactions.CountAsync();
                data.PendingTransactions = await _context.Transactions
                    .AsNoTracking()
                    .CountAsync(t => t.Status == TransactionStatus.Pending);
                data.CompletedTransactions = await platformTransactions
                    .CountAsync(t => t.Type == PlatformTransactionType.Revenue);
                data.FailedTransactions = await _context.Transactions
                    .AsNoTracking()
                    .CountAsync(t => t.Status == TransactionStatus.Failed);

                // Revenus et finances: journal plateforme, pas transactions wallet utilisateur.
                data.TotalRevenue = await platformTransactions
                    .Where(t => t.Type == PlatformTransactionType.Revenue)
                    .SumAsync(t => t.Amount);

                var startOfMonth = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
                data.MonthlyRevenue = await platformTransactions
                    .Where(t => t.Type == PlatformTransactionType.Revenue && t.CreatedAt >= startOfMonth)
                    .SumAsync(t => t.Amount);

                // Commandes
                data.TotalOrders = await _context.Orders.CountAsync();
                data.PendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == OrderStatus.Pending);
                data.CompletedOrders = await _context.Orders
                    .CountAsync(o => o.Status == OrderStatus.Delivered);

                // Produits
                data.TotalProducts = await _context.Products.CountAsync(p => !p.IsDeleted);
                data.LowStockProducts = await _context.Stocks
                    .CountAsync(s => s.Quantity <= 5);

                // Entreprises
                data.TotalCompanies = await _context.Companies.CountAsync(c => !c.IsDeleted);
                data.VerifiedCompanies = await _context.Companies
                    .CountAsync(c => !c.IsDeleted && c.IsVerified);

                // Solde admin
                var adminUserId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.ReferralCode == "00000000")
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();
                
                data.AdminBalance = adminUserId != null 
                    ? await _context.Wallets.AsNoTracking().Where(w => w.UserId == adminUserId && w.Type == WalletType.Platform)
                        .Select(w => w.Balance)
                        .FirstOrDefaultAsync()
                    : 0;

                // Transactions récentes
                data.RecentTransactions = await _context.Transactions
                    .AsNoTracking()
                    .Include(t => t.User)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                // ============== CALCULS AMAZON-LIKE ==============
                
                // Paiements aux vendeurs
                var allSupplierPayments = await _context.SellerPayments
                    .AsNoTracking()
                    .ToListAsync();

                data.TotalSupplierPayments = await platformTransactions
                    .Where(t => t.Category == PlatformTransactionCategory.SellerPayment)
                    .SumAsync(t => Math.Abs(t.Amount));

                data.PendingSupplierPayments = allSupplierPayments
                    .Where(sp => sp.Status == PaymentStatus.Pending)
                    .Sum(sp => sp.SellerAmount ?? 0);

                // Commissions ambassadeurs
                var allAmbassadorCommissions = await _context.AmbassadorCommissions
                    .AsNoTracking()
                    .ToListAsync();

                data.TotalAmbassadorCommissions = await platformTransactions
                    .Where(t => t.Category == PlatformTransactionCategory.CommissionReferrer
                             || t.Category == PlatformTransactionCategory.AmbassadorCommission)
                    .SumAsync(t => Math.Abs(t.Amount));

                data.PendingAmbassadorCommissions = allAmbassadorCommissions
                    .Where(ac => ac.Status == AmbassadorCommissionStatus.Pending || 
                               ac.Status == AmbassadorCommissionStatus.Available ||
                               ac.Status == AmbassadorCommissionStatus.Approved)
                    .Sum(ac => ac.Amount);

                // Profit/solde net journalisé de la plateforme.
                data.TotalPlatformProfit = await platformTransactions.SumAsync(t => t.Amount);
                
                var monthlySupplierPayments = await platformTransactions
                    .Where(t => t.Category == PlatformTransactionCategory.SellerPayment && t.CreatedAt >= startOfMonth)
                    .SumAsync(t => Math.Abs(t.Amount));

                var monthlyAmbassadorCommissions = await platformTransactions
                    .Where(t => (t.Category == PlatformTransactionCategory.CommissionReferrer
                              || t.Category == PlatformTransactionCategory.AmbassadorCommission)
                             && t.CreatedAt >= startOfMonth)
                    .SumAsync(t => Math.Abs(t.Amount));

                data.MonthlyPlatformProfit = await platformTransactions
                    .Where(t => t.CreatedAt >= startOfMonth)
                    .SumAsync(t => t.Amount);

                // Marge moyenne plateforme
                var paymentsWithCommission = allSupplierPayments
                    .Where(sp => sp.PlatformCommission.HasValue && sp.PlatformCommission.Value > 0)
                    .ToList();

                data.AveragePlatformMargin = paymentsWithCommission.Any()
                    ? paymentsWithCommission.Average(sp => sp.PlatformCommissionRate)
                    : 0m;

                // Coût codes promo (basé sur PromoDiscountAmount dans SellerPayment)
                data.PromoCodesCost = allSupplierPayments
                    .Sum(sp => sp.PromoDiscountAmount);

                data.MonthlyPromoCodesCost = allSupplierPayments
                    .Where(sp => sp.CreatedAt >= startOfMonth)
                    .Sum(sp => sp.PromoDiscountAmount);

                // Données pour graphiques
                await LoadAdminChartDataAsync(data);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des données Admin Dashboard");
                return new AdminDashboardData();
            }
        }

        public async Task<SellerDashboardData> GetSellerDashboardDataAsync(string userId)
        {
            try
            {
                var data = new SellerDashboardData();

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.SellerId == userId && !c.IsDeleted);

                if (company == null)
                    return data;

                data.Company = company;

                // Commandes du fournisseur
                var orders = _context.OrderProducts
                    .Include(op => op.Order)
                    .Where(op => op.CompanyId == company.Id && 
                               op.Order.Status != OrderStatus.Cancelled);

                data.TotalOrders = await orders.CountAsync();
                data.CompletedOrders = await orders
                    .CountAsync(op => op.Order.Status == OrderStatus.Delivered);
                data.PendingOrders = await orders
                    .CountAsync(op => op.Order.Status == OrderStatus.Pending);

                // Ventes et revenus
                data.ProductsSold = await orders
                    .Where(op => op.Order.Status != OrderStatus.Pending)
                    .SumAsync(op => op.Quantity);

                data.TotalRevenue = await orders
                    .Where(op => op.Order.Status == OrderStatus.Delivered)
                    .SumAsync(op => op.Quantity * op.PriceAtOrder);

                data.MonthlyRevenue = await orders
                    .Where(op => op.Order.Status == OrderStatus.Delivered &&
                               op.Order.OrderDate >= DateTimeOffset.UtcNow.AddDays(-30))
                    .SumAsync(op => op.Quantity * op.PriceAtOrder);

                // Produits
                data.TotalProducts = await _context.Products
                    .CountAsync(p => p.CompanyId == company.Id && !p.IsDeleted);

                data.ActiveProducts = await _context.ProductVariants
                    .CountAsync(pv => pv.Product.CompanyId == company.Id && 
                                     pv.IsAvailable && !pv.IsDeleted);

                data.LowStockProducts = await _context.Stocks
                    .CountAsync(s => s.ProductVariant.Product.CompanyId == company.Id && 
                                   s.Quantity <= 5);

                // Vues et engagement
                data.ProductViews = await _context.Products.AsNoTracking().Where(p => p.CompanyId == company.Id)
                    .SumAsync(p => p.ProductView);

                // Évaluations
                var reviews = _context.ProductReviews.AsNoTracking().Where(r => r.Product.CompanyId == company.Id);
                
                data.TotalReviews = await reviews.CountAsync();
                data.AverageRating = data.TotalReviews > 0
                    ? await reviews.AverageAsync(r => (decimal)r.Rate)
                    : 0;

                // Paiements en attente (estimation basée sur les commandes livrées non payées)
                // Paiements en attente - utilisation du montant fournisseur
                data.PendingPayments = await _context.SellerPayments.AsNoTracking().Where(sp => sp.CompanyId == company.Id && sp.Status == PaymentStatus.Pending)
                    .SumAsync(sp => sp.SellerAmount ?? 0);

                // ============== CALCULS AMAZON-LIKE ==============
                
                // Récupérer tous les paiements vendeur
                var allSupplierPayments = await _context.SellerPayments
                    .AsNoTracking()
                    .Where(sp => sp.CompanyId == company.Id)
                    .ToListAsync();

                var monthlyPayments = allSupplierPayments
                    .Where(sp => sp.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-30))
                    .ToList();

                // Revenus nets (montant réellement reçu après commissions)
                data.NetEarnings = allSupplierPayments
                    .Where(sp => sp.IsPaidToSeller)
                    .Sum(sp => sp.SellerAmount ?? 0);

                data.NetEarningsThisMonth = monthlyPayments
                    .Where(sp => sp.IsPaidToSeller)
                    .Sum(sp => sp.SellerAmount ?? 0);

                // Commissions plateforme totales
                data.PlatformCommissionTotal = allSupplierPayments
                    .Sum(sp => sp.PlatformCommission ?? 0);

                data.PlatformCommissionThisMonth = monthlyPayments
                    .Sum(sp => sp.PlatformCommission ?? 0);

                // Taux de commission moyen appliqué
                var paymentsWithRate = allSupplierPayments
                    .Where(sp => sp.PlatformCommissionRate > 0)
                    .ToList();
                
                data.AverageCommissionRate = paymentsWithRate.Any()
                    ? paymentsWithRate.Average(sp => sp.PlatformCommissionRate)
                    : 0m;

                // Détails paiements
                data.PaidAmount = data.NetEarnings;
                data.ApprovedAmount = allSupplierPayments
                    .Where(sp => sp.Status == PaymentStatus.Completed && !sp.IsPaidToSeller)
                    .Sum(sp => sp.SellerAmount ?? 0);
                
                data.PendingPaymentsCount = allSupplierPayments
                    .Count(sp => sp.Status == PaymentStatus.Pending);

                // Statistiques avancées
                data.AverageOrderValue = data.CompletedOrders > 0
                    ? data.TotalRevenue / data.CompletedOrders
                    : 0;

                data.ConversionRate = data.ProductViews > 0
                    ? ((decimal)data.ProductsSold / data.ProductViews) * 100
                    : 0;

                // Données pour graphiques
                await LoadSupplierChartDataAsync(data, company.Id);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des données Seller Dashboard pour {UserId}", userId);
                return new SellerDashboardData();
            }
        }

        public async Task<EmployeeDashboardData> GetEmployeeDashboardDataAsync(string userId)
        {
            try
            {
                var data = new EmployeeDashboardData();

                var employee = await _context.Employees.AsNoTracking().Include(e => e.Post)
                    .Include(e => e.Currency)
                    .FirstOrDefaultAsync(e => e.UserId == userId && !e.IsDeleted);

                if (employee == null)
                    return data;

                data.Employee = employee;

                // Tâches
                var tasks = _context.EmployeeTasks.AsNoTracking().Where(t => t.EmployeeId == employee.Id && !t.IsDeleted);

                data.TotalTasks = await tasks.CountAsync();
                data.CompletedTasks = await tasks
                    .CountAsync(t => t.Status == Models.TaskStatus.Completed);
                data.PendingTasks = await tasks
                    .CountAsync(t => t.Status == Models.TaskStatus.Pending);
                data.InProgressTasks = await tasks
                    .CountAsync(t => t.Status == Models.TaskStatus.InProgress);

                // Performances
                var performances = _context.EmployeePerformances.AsNoTracking().Where(p => p.EmployeeId == employee.Id && !p.IsDeleted);

                data.AveragePerformance = await performances.AnyAsync()
                    ? await performances.AverageAsync(p => p.Score)
                    : 0;

                var currentMonth = DateTimeOffset.UtcNow.Month;
                var currentYear = DateTimeOffset.UtcNow.Year;

                // Performance mensuelle - EvaluationDate est DateTimeOffset non-nullable
                var monthlyPerfs = await performances
                    .Where(p => p.EvaluationDate.Month == currentMonth &&
                               p.EvaluationDate.Year == currentYear)
                    .ToListAsync();
                
                data.MonthlyPerformance = monthlyPerfs.Any() 
                    ? monthlyPerfs.Average(p => p.Score)
                    : 0;

                // Présence (si le modèle EmployeeAttendance existe)
                data.AttendanceRate = await CalculateAttendanceRateAsync(employee.Id);

                // Heures de travail estimées depuis les tâches complétées.
                data.MonthlyHours = await CalculateMonthlyHoursAsync(employee.Id);
                data.WeeklyHours = await CalculateWeeklyHoursAsync(employee.Id);

                // Calculs salaire
                data.BaseSalary = employee.Salary;
                data.BonusAmount = CalculateBonusAmount(data.AveragePerformance, data.BaseSalary);
                data.EstimatedSalary = data.BaseSalary + data.BonusAmount;

                // Tâches récentes
                data.RecentTasks = await tasks
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Données pour graphiques
                await LoadEmployeeChartDataAsync(data, employee.Id);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des données Employee Dashboard pour {UserId}", userId);
                return new EmployeeDashboardData();
            }
        }

        private async Task LoadAdminChartDataAsync(AdminDashboardData data)
        {
            // Revenus des 6 derniers mois
            var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-5);
            var firstChartMonth = new DateTimeOffset(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var chartMonths = Enumerable.Range(0, 6)
                .Select(i => firstChartMonth.AddMonths(i))
                .ToList();

            var monthlyRevenues = await _context.PlatformTransactions.AsNoTracking()
                .Where(t => !t.IsDeleted
                    && t.Type == PlatformTransactionType.Revenue
                    && t.CreatedAt >= firstChartMonth)
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Sum(t => t.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            var monthlyTransactionCounts = await _context.PlatformTransactions.AsNoTracking()
                .Where(t => !t.IsDeleted && t.CreatedAt >= firstChartMonth)
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.MonthlyRevenueLabels = chartMonths
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy"))
                .ToList();
            data.MonthlyRevenueValues = chartMonths
                .Select(month => monthlyRevenues.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();
            data.MonthlyTransactionCountValues = chartMonths
                .Select(month => monthlyTransactionCounts.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            // Nouveaux utilisateurs par mois
            var monthlyUsers = await _context.Users.AsNoTracking().Where(u => !u.IsDeleted && u.RegistrationDate >= sixMonthsAgo)
                .GroupBy(u => new { u.RegistrationDate.Year, u.RegistrationDate.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.MonthlyUsersLabels = data.MonthlyRevenueLabels.ToList();
            data.MonthlyUsersValues = chartMonths
                .Select(month => monthlyUsers.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            // ============== NOUVEAU : Profit plateforme mensuel ==============
            var monthlyProfits = new List<MonthlyData>();
            
            for (int i = 5; i >= 0; i--)
            {
                var monthDate = DateTimeOffset.UtcNow.AddMonths(-i);
                var monthStart = new DateTimeOffset(monthDate.Year, monthDate.Month, 1, 0, 0, 0, TimeSpan.Zero);
                var monthEnd = monthStart.AddMonths(1);

                // Solde net journalisé du mois
                var monthNet = await _context.PlatformTransactions
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted &&
                              t.CreatedAt >= monthStart &&
                              t.CreatedAt < monthEnd)
                    .SumAsync(t => t.Amount);

                monthlyProfits.Add(new MonthlyData
                {
                    Year = monthDate.Year,
                    Month = monthDate.Month,
                    Value = monthNet
                });
            }

            data.MonthlyProfitLabels = monthlyProfits
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy"))
                .ToList();
            data.MonthlyProfitValues = monthlyProfits.Select(x => x.Value).ToList();

            // Répartition des types de transactions
            var transactionsByType = await _context.PlatformTransactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .GroupBy(t => t.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            data.TransactionTypeValues = new List<int>
            {
                transactionsByType.Where(t => t.Category == PlatformTransactionCategory.UserDeposit
                                           || t.Category == PlatformTransactionCategory.OrderPayment
                                           || t.Category == PlatformTransactionCategory.SubscriptionPayment
                                           || t.Category == PlatformTransactionCategory.OtherIncome).Sum(t => t.Count),
                transactionsByType.Where(t => t.Category == PlatformTransactionCategory.UserWithdrawal
                                           || t.Category == PlatformTransactionCategory.SellerPayment
                                           || t.Category == PlatformTransactionCategory.Refund).Sum(t => t.Count),
                transactionsByType.Where(t => t.Category == PlatformTransactionCategory.BonusPayment
                                           || t.Category == PlatformTransactionCategory.CommissionReferrer
                                           || t.Category == PlatformTransactionCategory.AmbassadorCommission).Sum(t => t.Count),
                transactionsByType.Where(t => t.Category != PlatformTransactionCategory.UserDeposit
                                           && t.Category != PlatformTransactionCategory.OrderPayment
                                           && t.Category != PlatformTransactionCategory.SubscriptionPayment
                                           && t.Category != PlatformTransactionCategory.OtherIncome
                                           && t.Category != PlatformTransactionCategory.UserWithdrawal
                                           && t.Category != PlatformTransactionCategory.SellerPayment
                                           && t.Category != PlatformTransactionCategory.Refund
                                           && t.Category != PlatformTransactionCategory.BonusPayment
                                           && t.Category != PlatformTransactionCategory.CommissionReferrer
                                           && t.Category != PlatformTransactionCategory.AmbassadorCommission).Sum(t => t.Count)
            };

            // Répartition des statuts de commandes
            data.OrderStatusValues = new List<int>
            {
                data.PendingOrders,
                await _context.Orders.CountAsync(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.PartiallyShipped),
                data.CompletedOrders
            };
        }

        private async Task LoadSupplierChartDataAsync(SellerDashboardData data, int companyId)
        {
            // Ventes des 6 derniers mois
            var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-5);
            var firstChartMonth = new DateTimeOffset(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var chartMonths = Enumerable.Range(0, 6)
                .Select(i => firstChartMonth.AddMonths(i))
                .ToList();

            var monthlySales = await _context.OrderProducts.AsNoTracking().Include(op => op.Order)
                .Where(op => op.CompanyId == companyId && 
                           op.Order.Status == OrderStatus.Delivered &&
                           op.Order.OrderDate >= firstChartMonth)
                .GroupBy(op => new { op.Order.OrderDate.Year, op.Order.OrderDate.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Sum(op => op.Quantity * op.PriceAtOrder)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.SalesLabels = chartMonths
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy"))
                .ToList();
            data.SalesValues = chartMonths
                .Select(month => monthlySales.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            var monthlyOrderCounts = await _context.OrderProducts
                .AsNoTracking()
                .Include(op => op.Order)
                .Where(op => op.CompanyId == companyId
                    && op.Order.Status != OrderStatus.Pending
                    && op.Order.Status != OrderStatus.Cancelled
                    && op.Order.OrderDate >= firstChartMonth)
                .GroupBy(op => new { op.Order.OrderDate.Year, op.Order.OrderDate.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Select(op => op.OrderId).Distinct().Count()
                })
                .ToListAsync();

            data.OrderCountValues = chartMonths
                .Select(month => monthlyOrderCounts.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            var monthlyProductsSold = await _context.OrderProducts
                .AsNoTracking()
                .Include(op => op.Order)
                .Where(op => op.CompanyId == companyId
                    && op.Order.Status != OrderStatus.Pending
                    && op.Order.Status != OrderStatus.Cancelled
                    && op.Order.OrderDate >= firstChartMonth)
                .GroupBy(op => new { op.Order.OrderDate.Year, op.Order.OrderDate.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Sum(op => op.Quantity)
                })
                .ToListAsync();

            data.ProductsSoldValues = chartMonths
                .Select(month => monthlyProductsSold.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            // ============== NOUVEAU : Revenus nets (après commissions) ==============
            var monthlyNetEarnings = await _context.SellerPayments
                .AsNoTracking()
                .Where(sp => sp.CompanyId == companyId && 
                           sp.IsPaidToSeller &&
                           sp.PaymentDate >= firstChartMonth)
                .GroupBy(sp => new { sp.PaymentDate.Year, sp.PaymentDate.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Sum(sp => sp.SellerAmount ?? 0)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.NetEarningsLabels = data.SalesLabels.ToList();
            data.NetEarningsValues = chartMonths
                .Select(month => monthlyNetEarnings.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            // Répartition par catégorie
            var categoryDistribution = await _context.OrderProducts.AsNoTracking().Include(op => op.Order)
                .Include(op => op.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                        .ThenInclude(p => p.Category)
                .Where(op => op.CompanyId == companyId && op.Order.Status == OrderStatus.Delivered)
                .GroupBy(op => op.ProductVariant.Product.Category.Name ?? "Sans catégorie")
                .Select(g => new CategoryData
                {
                    Category = g.Key,
                    Value = g.Sum(op => op.Quantity * op.PriceAtOrder)
                })
                .ToListAsync();

            data.CategoryLabels = categoryDistribution.Select(x => x.Category).ToList();
            data.CategoryValues = categoryDistribution.Select(x => x.Value).ToList();

            // Répartition des statuts de commandes
            data.OrderStatusValues = new List<int>
            {
                data.PendingOrders,
                await _context.OrderProducts.AsNoTracking().Include(op => op.Order)
                    .CountAsync(op => op.CompanyId == companyId && (op.Order.Status == OrderStatus.Paid || op.Order.Status == OrderStatus.PartiallyShipped)),
                data.CompletedOrders
            };

            // Produits en stock faible - pour affichage en tableau
            var lowStockProducts = await _context.Products.AsNoTracking().Include(p => p.Variants)
                    .ThenInclude(pv => pv.Stock)
                .Where(p => p.CompanyId == companyId && 
                           !p.IsDeleted)
                .ToListAsync();

            data.LowStockProductsList = lowStockProducts
                .Where(p => p.Variants != null && p.Variants.Any(v => v.Stock != null && v.Stock.Quantity <= 5))
                .Take(5)
                .ToList();

            // Commandes récentes
            data.RecentOrders = await _context.Orders.AsNoTracking().Where(o => o.Products.Any(op => op.CompanyId == companyId))
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();
        }

        private async Task LoadEmployeeChartDataAsync(EmployeeDashboardData data, int employeeId)
        {
            // Performance des 6 derniers mois
            var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-5);
            var monthlyPerformances = await _context.EmployeePerformances.AsNoTracking().Where(p => p.EmployeeId == employeeId && !p.IsDeleted &&
                           p.EvaluationDate >= sixMonthsAgo)
                .GroupBy(p => new { p.EvaluationDate.Year, p.EvaluationDate.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Average(p => p.Score)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.PerformanceLabels = monthlyPerformances
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy"))
                .ToList();
            data.PerformanceValues = monthlyPerformances.Select(x => x.Value).ToList();

            // Répartition des tâches par statut
            var taskDistribution = await _context.EmployeeTasks.AsNoTracking().Where(t => t.EmployeeId == employeeId && !t.IsDeleted)
                .GroupBy(t => t.Status)
                .Select(g => new
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();

            data.TaskStatusLabels = new List<string> { "En attente", "En cours", "Terminées" };
            data.TaskStatusValues = new List<int>
            {
                taskDistribution.FirstOrDefault(t => t.Status == Models.TaskStatus.Pending.ToString())?.Count ?? 0,
                taskDistribution.FirstOrDefault(t => t.Status == Models.TaskStatus.InProgress.ToString())?.Count ?? 0,
                taskDistribution.FirstOrDefault(t => t.Status == Models.TaskStatus.Completed.ToString())?.Count ?? 0
            };

            // Heures travaillées par mois - 6 derniers mois
            var hoursData = new List<MonthlyData>();
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTimeOffset.UtcNow.AddMonths(-i);
                var tasksCompleted = await _context.EmployeeTasks.AsNoTracking().Where(t => t.EmployeeId == employeeId &&
                               t.Status == Models.TaskStatus.Completed &&
                               t.UpdatedAt.HasValue &&
                               t.UpdatedAt.Value.Month == month.Month &&
                               t.UpdatedAt.Value.Year == month.Year)
                    .CountAsync();

                hoursData.Add(new MonthlyData
                {
                    Year = month.Year,
                    Month = month.Month,
                    Value = tasksCompleted * 4m // 4 heures par tâche
                });
            }

            data.HoursLabels = hoursData
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy"))
                .ToList();
            data.HoursValues = hoursData.Select(x => x.Value).ToList();
        }

        private async Task<decimal> CalculateAttendanceRateAsync(int employeeId)
        {
            var currentMonth = DateTimeOffset.UtcNow.Month;
            var currentYear = DateTimeOffset.UtcNow.Year;

            // Vérifier si le modèle EmployeeAttendance existe
            try
            {
                var totalDays = await _context.Set<EmployeeAttendance>()
                    .Where(a => a.EmployeeId == employeeId && 
                               a.Date.Month == currentMonth && 
                               a.Date.Year == currentYear)
                    .CountAsync();

                var presentDays = await _context.Set<EmployeeAttendance>()
                    .Where(a => a.EmployeeId == employeeId && 
                               a.Date.Month == currentMonth && 
                               a.Date.Year == currentYear &&
                               a.Status == Models.AttendanceStatus.Present)
                    .CountAsync();

                return totalDays > 0 ? (decimal)presentDays / totalDays * 100 : 0m;
            }
            catch
            {
                return 0m;
            }
        }

        private async Task<decimal> CalculateMonthlyHoursAsync(int employeeId)
        {
            // Estimation minimale basée sur les tâches complétées tant qu'aucune feuille d'heures dédiée n'existe.
            var currentMonth = DateTimeOffset.UtcNow.Month;
            var currentYear = DateTimeOffset.UtcNow.Year;

            var tasksThisMonth = await _context.EmployeeTasks.AsNoTracking().Where(t => t.EmployeeId == employeeId &&
                                t.Status == Models.TaskStatus.Completed &&
                                t.UpdatedAt.HasValue &&
                                t.UpdatedAt.Value.Month == currentMonth &&
                                t.UpdatedAt.Value.Year == currentYear)
                    .CountAsync();

            return tasksThisMonth * 4m;
        }

        private async Task<decimal> CalculateWeeklyHoursAsync(int employeeId)
        {
            var weekStart = DateTimeOffset.UtcNow.AddDays(-(int)DateTimeOffset.UtcNow.DayOfWeek);
            
            var tasksThisWeek = await _context.EmployeeTasks.AsNoTracking().Where(t => t.EmployeeId == employeeId && 
                           t.Status == Models.TaskStatus.Completed &&
                           t.UpdatedAt >= weekStart)
                .CountAsync();

            return tasksThisWeek * 4m;
        }

        private static decimal CalculateBonusAmount(decimal performanceScore, decimal baseSalary)
        {
            if (performanceScore <= 0) return 0;
            
            // Bonus = 10% du salaire de base si performance > 80%
            if (performanceScore >= 80)
                return baseSalary * 0.1m;
            
            // Bonus proportionnel pour performance entre 60% et 80%
            if (performanceScore >= 60)
                return baseSalary * 0.05m;
                
            return 0;
        }

        public async Task<UserDashboardData> GetUserDashboardDataAsync(string userId)
        {
            try
            {
                var data = new UserDashboardData();

                // Charger l'utilisateur
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return data;

                data.User = user;

                // Charger le portefeuille
                data.CurrentWallet = await _context.Wallets.AsNoTracking().Include(w => w.Currency)
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.Type == WalletType.Customer);

                data.EarningsWallet = await _context.Wallets.AsNoTracking().Include(w => w.Currency)
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.Type == WalletType.Earnings);

                // Charger l'abonnement actif
                var activeSubscription = await _context.AmbassadorSubscriptions
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.UserId == userId
                        && s.Status == SubscriptionStatus.Active
                        && s.EndDate > DateTimeOffset.UtcNow
                        && !s.IsDeleted);

                data.ActiveSubscription = activeSubscription;

                if (activeSubscription != null)
                {
                    data.CurrentPlan = activeSubscription.Plan;
                }

                // Charger les parrainages
                data.Referrals = await _context.Referrals.AsNoTracking().Where(r => r.ReferrerId == userId)
                    .ToListAsync();

                // Parrainages ce mois
                var currentMonth = DateTimeOffset.UtcNow.Month;
                var currentYear = DateTimeOffset.UtcNow.Year;
                data.NewReferralsThisMonth = data.Referrals
                    .Count(r => r.CreatedAt.Month == currentMonth && r.CreatedAt.Year == currentYear);

                data.ReferralTarget = 0;
                data.CurrentReferrals = data.Referrals.Count;

                data.TotalGains = await _context.AmbassadorCommissions
                    .AsNoTracking()
                    .Where(c => c.AmbassadorId == userId && c.Status == AmbassadorCommissionStatus.Paid)
                    .SumAsync(c => c.Amount);

                data.CurrentSalesCommission = await _context.AmbassadorCommissions
                    .AsNoTracking()
                    .Where(c => c.AmbassadorId == userId
                        && c.Status == AmbassadorCommissionStatus.Paid
                        && c.PaidAt.HasValue
                        && c.PaidAt.Value.Month == currentMonth
                        && c.PaidAt.Value.Year == currentYear)
                    .SumAsync(c => c.Amount);
                data.SalesTarget = 0m;

                // Soldes portefeuille
                if (data.CurrentWallet != null)
                {
                    data.AvailableBalance = data.CurrentWallet.Balance;
                }

                data.EarningsBalance = data.EarningsWallet?.Balance ?? 0m;

                data.PendingBalance = await _context.AmbassadorCommissions
                    .AsNoTracking()
                    .Where(c => c.AmbassadorId == userId
                        && (c.Status == AmbassadorCommissionStatus.Pending
                            || c.Status == AmbassadorCommissionStatus.Available
                            || c.Status == AmbassadorCommissionStatus.Approved))
                    .SumAsync(c => c.Amount);

                // Données client e-commerce
                data.TotalOrders = await _context.Orders
                    .AsNoTracking()
                    .CountAsync(o => o.UserId == userId && !o.IsDeleted);

                data.PendingPaymentOrders = await _context.Orders
                    .AsNoTracking()
                    .CountAsync(o => o.UserId == userId && !o.IsDeleted && o.Status == OrderStatus.Pending);

                data.ActiveOrders = await _context.Orders
                    .AsNoTracking()
                    .CountAsync(o => o.UserId == userId
                        && !o.IsDeleted
                        && o.Status != OrderStatus.Delivered
                        && o.Status != OrderStatus.Cancelled
                        && o.Status != OrderStatus.Refunded);

                data.RecentOrders = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Currency)
                    .Include(o => o.Products)
                        .ThenInclude(op => op.ProductVariant)
                            .ThenInclude(v => v.Product)
                    .Where(o => o.UserId == userId && !o.IsDeleted)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync();

                data.PrimaryAddress = await _context.Addresses
                    .AsNoTracking()
                    .Include(a => a.Country)
                    .Include(a => a.State)
                    .Include(a => a.City)
                    .Include(a => a.Neighborhood)
                    .Where(a => a.UserId == userId && !a.IsDeleted)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.ModifiedAt)
                    .FirstOrDefaultAsync();

                // Données pour graphiques (6 derniers mois)
                await LoadUserChartDataAsync(data, userId);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des données User Dashboard pour {UserId}", userId);
                return new UserDashboardData();
            }
        }

        private async Task LoadUserChartDataAsync(UserDashboardData data, string userId)
        {
            // Commissions des 6 derniers mois
            var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-5);
            var firstChartMonth = new DateTimeOffset(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var chartMonths = Enumerable.Range(0, 6)
                .Select(i => firstChartMonth.AddMonths(i))
                .ToList();
            
            var monthlyCommissions = await _context.AmbassadorCommissions.AsNoTracking()
                .Where(c => c.AmbassadorId == userId
                    && c.Status == AmbassadorCommissionStatus.Paid
                    && c.PaidAt.HasValue
                    && c.PaidAt.Value >= firstChartMonth)
                .GroupBy(c => new { c.PaidAt.Value.Year, c.PaidAt.Value.Month })
                .Select(g => new MonthlyData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Value = g.Sum(c => c.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.CommissionLabels = chartMonths
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM yy"))
                .ToList();
            data.CommissionValues = chartMonths
                .Select(month => monthlyCommissions.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Value ?? 0m)
                .ToList();

            // Parrainages des 6 derniers mois
            var monthlyReferrals = await _context.Referrals.AsNoTracking().Where(r => r.ReferrerId == userId && r.CreatedAt >= firstChartMonth)
                .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            data.ReferralLabels = data.CommissionLabels.ToList();
            data.ReferralValues = chartMonths
                .Select(month => monthlyReferrals.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Count ?? 0)
                .ToList();

            // Statut des transactions du portefeuille
            var transactionsByStatus = await _context.Transactions.AsNoTracking().Where(t => t.UserId == userId)
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            data.TransactionStatusValues = new List<int>
            {
                transactionsByStatus.FirstOrDefault(t => t.Status == TransactionStatus.Completed)?.Count ?? 0,
                transactionsByStatus.FirstOrDefault(t => t.Status == TransactionStatus.Pending)?.Count ?? 0,
                transactionsByStatus.FirstOrDefault(t => t.Status == TransactionStatus.Failed)?.Count ?? 0
            };

            // Transactions récentes
            data.RecentTransactions = await _context.Transactions.AsNoTracking().Include(t => t.User)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();
        }

    }

    // Classes pour les données des dashboards
    
    public class UserDashboardData
    {
        public ApplicationUser User { get; set; }
        public Wallet CurrentWallet { get; set; }
        public Wallet EarningsWallet { get; set; }
        public AmbassadorSubscription ActiveSubscription { get; set; }
        public AmbassadorPlan CurrentPlan { get; set; }
        public decimal TotalGains { get; set; }
        public List<Referral> Referrals { get; set; } = new();
        public List<Transaction> RecentTransactions { get; set; } = new();
        
        // Objectifs et progress
        public decimal SalesTarget { get; set; }
        public decimal CurrentSalesCommission { get; set; }
        public int ReferralTarget { get; set; }
        public int CurrentReferrals { get; set; }
        public int NewReferralsThisMonth { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal EarningsBalance { get; set; }
        public decimal PendingBalance { get; set; }

        // Données client e-commerce
        public int TotalOrders { get; set; }
        public int ActiveOrders { get; set; }
        public int PendingPaymentOrders { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
        public Address PrimaryAddress { get; set; }
        
        // Graphiques - Commissions
        public List<string> CommissionLabels { get; set; } = new();
        public List<decimal> CommissionValues { get; set; } = new();
        
        // Graphiques - Parrainages
        public List<string> ReferralLabels { get; set; } = new();
        public List<int> ReferralValues { get; set; } = new();
        
        // Graphiques - Statut transactions
        public List<string> TransactionStatusLabels { get; set; } = new() { "Complétée", "En attente", "Échouée" };
        public List<int> TransactionStatusValues { get; set; } = new();
    }

    public class AdminDashboardData
    {
        public int TotalUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int InactiveSubscriptions { get; set; }
        public int CancelledSubscriptions { get; set; }
        public int TotalTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int TotalProducts { get; set; }
        public int TotalPlans { get; set; }
        public int LowStockProducts { get; set; }
        public int TotalCompanies { get; set; }
        public int VerifiedCompanies { get; set; }
        public decimal AdminBalance { get; set; }
        public List<Transaction> RecentTransactions { get; set; } = new();
        
        // ============== NOUVELLES DONNÉES AMAZON-LIKE ==============
        
        // Finances plateforme
        public decimal TotalPlatformProfit { get; set; }               // Profit total plateforme
        public decimal MonthlyPlatformProfit { get; set; }             // Profit ce mois
        public decimal TotalSupplierPayments { get; set; }             // Total payé aux vendeurs
        public decimal TotalAmbassadorCommissions { get; set; }        // Total commissions ambassadeurs
        public decimal PendingSupplierPayments { get; set; }           // Paiements vendeurs en attente
        public decimal PendingAmbassadorCommissions { get; set; }      // Commissions ambassadeurs en attente
        
        // Statistiques financières avancées
        public decimal AveragePlatformMargin { get; set; }             // Marge moyenne plateforme
        public decimal PromoCodesCost { get; set; }                    // Coût total codes promo
        public decimal MonthlyPromoCodesCost { get; set; }             // Coût promo ce mois
        
        // Données pour graphiques - Revenus
        public List<string> MonthlyRevenueLabels { get; set; } = new();
        public List<decimal> MonthlyRevenueValues { get; set; } = new();
        public List<decimal> MonthlyTransactionCountValues { get; set; } = new();
        public List<string> MonthlyUsersLabels { get; set; } = new();
        public List<decimal> MonthlyUsersValues { get; set; } = new();
        
        // NOUVEAU - Profit plateforme
        public List<string> MonthlyProfitLabels { get; set; } = new();
        public List<decimal> MonthlyProfitValues { get; set; } = new();
        
        // Données pour graphiques - Répartition transactions
        public List<string> TransactionTypeLabels { get; set; } = new() { "Dépôts", "Retraits", "Bonus", "Autres" };
        public List<int> TransactionTypeValues { get; set; } = new();
        
        // Données pour graphiques - Répartition commandes
        public List<string> OrderStatusLabels { get; set; } = new() { "En attente", "En cours", "Livrées" };
        public List<int> OrderStatusValues { get; set; } = new();
    }

    public class SellerDashboardData
    {
        public Company Company { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int PendingOrders { get; set; }
        public int ProductsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int LowStockProducts { get; set; }
        public int ProductViews { get; set; }
        public int TotalReviews { get; set; }
        public decimal AverageRating { get; set; }
        public decimal PendingPayments { get; set; }
        public List<Product> LowStockProductsList { get; set; } = new();
        public List<Order> RecentOrders { get; set; } = new();
        
        // ============== NOUVELLES DONNÉES AMAZON-LIKE ==============
        
        // Revenus nets (après commissions)
        public decimal NetEarnings { get; set; }                    // Total net reçu
        public decimal NetEarningsThisMonth { get; set; }           // Net ce mois
        public decimal PlatformCommissionTotal { get; set; }        // Total commissions plateforme
        public decimal PlatformCommissionThisMonth { get; set; }    // Commission plateforme ce mois
        public decimal AverageCommissionRate { get; set; }          // Taux moyen appliqué
        
        // Détails paiements
        public decimal PaidAmount { get; set; }                     // Total payé
        public decimal ApprovedAmount { get; set; }                 // Approuvé en attente paiement
        public int PendingPaymentsCount { get; set; }               // Nombre paiements en attente
        
        // Statistiques avancées
        public decimal AverageOrderValue { get; set; }              // Valeur moyenne commande
        public decimal ConversionRate { get; set; }                 // Taux conversion vues/ventes
        
        // Données pour graphiques - Ventes
        public List<string> SalesLabels { get; set; } = new();
        public List<decimal> SalesValues { get; set; } = new();
        public List<decimal> OrderCountValues { get; set; } = new();
        public List<decimal> ProductsSoldValues { get; set; } = new();
        
        // NOUVEAU - Revenus nets pour graphique
        public List<string> NetEarningsLabels { get; set; } = new();
        public List<decimal> NetEarningsValues { get; set; } = new();
        
        // Données pour graphiques - Répartition catégories
        public List<string> CategoryLabels { get; set; } = new();
        public List<decimal> CategoryValues { get; set; } = new();
        
        // Données pour graphiques - Statuts commandes
        public List<string> OrderStatusLabels { get; set; } = new() { "En attente", "En cours", "Livrées" };
        public List<int> OrderStatusValues { get; set; } = new();
    }

    public class EmployeeDashboardData
    {
        public Employee Employee { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public decimal AveragePerformance { get; set; }
        public decimal MonthlyPerformance { get; set; }
        public decimal AttendanceRate { get; set; }
        public decimal MonthlyHours { get; set; }
        public decimal WeeklyHours { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal BonusAmount { get; set; }
        public decimal EstimatedSalary { get; set; }
        public List<EmployeeTask> RecentTasks { get; set; } = new();
        public EmployeeAttendance TodayAttendance { get; set; }
        
        // Données pour graphiques - Performance
        public List<string> PerformanceLabels { get; set; } = new();
        public List<decimal> PerformanceValues { get; set; } = new();
        
        // Données pour graphiques - Statut tâches
        public List<string> TaskStatusLabels { get; set; } = new() { "En attente", "En cours", "Terminées" };
        public List<int> TaskStatusValues { get; set; } = new();
        
        // Données pour graphiques - Heures travaillées
        public List<string> HoursLabels { get; set; } = new();
        public List<decimal> HoursValues { get; set; } = new();
    }

    // Classes utilitaires
    public class MonthlyData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Value { get; set; }
    }

    public class CategoryData
    {
        public string Category { get; set; }
        public decimal Value { get; set; }
    }
}

