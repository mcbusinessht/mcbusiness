using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Models;
using MCBusiness.Filters;

namespace MCBusiness.Pages.Users.Admin.FinancialReconciliation
{
    [Authorize]
    [TypeFilter(typeof(AdminAccessFilter))]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        // Propriétés principales
        public decimal TotalRevenue { get; set; }
        public int AuditedTransactionsCount { get; set; }
        public int UnauditedTransactionsCount { get; set; }
        public decimal AuditCoveragePercent { get; set; }
        public decimal TodayRevenue { get; set; }
        public int TodayTransactionsCount { get; set; }

        // Totaux issus des PlatformTransaction (audit) pour visualisation et cohérence
        public decimal TotalRevenueFromPT { get; set; }
        public decimal TotalExpenseFromPT { get; set; }
        public decimal NetBalanceFromPT => TotalRevenueFromPT + TotalExpenseFromPT; // Expense stockés négatifs
        public decimal PlatformWalletBalance { get; set; }
        public bool PlatformBalanceMatchesPT { get; set; }
        
        // Filtres
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string AuditStatus { get; set; }
        
        // Données détaillées
        public List<TransactionReconciliationDto> FilteredTransactions { get; set; } = new();
        public List<PaymentMethodSummaryDto> PaymentMethodSummary { get; set; } = new();
        public List<string> ReconciliationIssues { get; set; } = new();
        public bool HasReconciliationIssues => ReconciliationIssues.Any();

        public async Task<IActionResult> OnGetAsync(DateTime? startDate = null, DateTime? endDate = null, string auditStatus = null)
        {
            StartDate = (startDate ?? DateTime.Today.AddDays(-30))
                .Date
                .ToUniversalTime();

            EndDate = (endDate ?? DateTime.Today)
                .Date
                .AddDays(1)
                .AddTicks(-1)
                .ToUniversalTime();
            AuditStatus = auditStatus;

            await LoadReconciliationDataAsync();
            await LoadPlatformAuditTotalsAsync();
            await LoadPaymentMethodSummaryAsync();
            await LoadTransactionDetailsAsync();
            await CheckReconciliationIssuesAsync();

            return Page();
        }

        private async Task LoadReconciliationDataAsync()
        {
            // Revenus totaux depuis le début (tous les Payment complétés)
            var allPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .ToListAsync();

            TotalRevenue = allPayments.Sum(p => p.Amount);

            // Audité = présence d'une PlatformTransaction du bon type pour ce paiement
            // Commandes : Category OrderPayment et même OrderId
            // Abonnements ambassadeur : Category SubscriptionPayment et même AmbassadorSubscriptionId
            var auditedOrderPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.OrderId.HasValue &&
                           _context.PlatformTransactions.Any(pt => pt.OrderId == p.OrderId &&
                                                                  pt.Category == PlatformTransactionCategory.OrderPayment &&
                                                                  !pt.IsDeleted))
                .CountAsync();
            var auditedSubscriptionPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.AmbassadorSubscriptionId.HasValue &&
                           _context.PlatformTransactions.Any(pt => pt.AmbassadorSubscriptionId == p.AmbassadorSubscriptionId &&
                                                                  pt.Category == PlatformTransactionCategory.SubscriptionPayment &&
                                                                  !pt.IsDeleted))
                .CountAsync();

            var auditedPayments = auditedOrderPayments + auditedSubscriptionPayments;
            AuditedTransactionsCount = auditedPayments;
            UnauditedTransactionsCount = allPayments.Count - auditedPayments;
            AuditCoveragePercent = allPayments.Count > 0
                ? (decimal)auditedPayments / allPayments.Count * 100
                : 0;

            // Métriques du jour
            var today = DateTime.Today;
            var todayPayments = allPayments.Where(p => p.CreatedAt.Date == today).ToList();
            TodayRevenue = todayPayments.Sum(p => p.Amount);
            TodayTransactionsCount = todayPayments.Count;
        }

        private async Task LoadPlatformAuditTotalsAsync()
        {
            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == "00000000");
            if (platformUser == null) return;

            var platformWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == platformUser.Id && w.Type == WalletType.Platform);
            if (platformWallet != null)
                PlatformWalletBalance = platformWallet.Balance;

            var ptQuery = _context.PlatformTransactions.Where(pt => !pt.IsDeleted);
            TotalRevenueFromPT = await ptQuery.Where(pt => pt.Type == PlatformTransactionType.Revenue).SumAsync(pt => pt.Amount);
            TotalExpenseFromPT = await ptQuery.Where(pt => pt.Type == PlatformTransactionType.Expense).SumAsync(pt => pt.Amount);
            var netFromPT = TotalRevenueFromPT + TotalExpenseFromPT; // dépenses déjà négatives
            PlatformBalanceMatchesPT = Math.Abs(PlatformWalletBalance - netFromPT) <= 0.01m;
        }

        private async Task LoadPaymentMethodSummaryAsync()
        {
            var payments = await _context.Payments
                .Where(p =>
                    p.Status == PaymentStatus.Completed &&
                    p.CreatedAt >= StartDate.Value &&
                    p.CreatedAt <= EndDate.Value
                )
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new 
                {
                    Method = g.Key,
                    Amount = g.Sum(p => p.Amount),
                    Count = g.Count(),
                    PaymentIds = g.Select(p => p.Id).ToList()
                })
                .ToListAsync();

            PaymentMethodSummary = new List<PaymentMethodSummaryDto>();

            foreach (var payment in payments)
            {
                // Nombre de paiements de cette méthode qui ont un audit (OrderPayment ou SubscriptionPayment)
                var auditedCount = await _context.Payments
                    .Where(p => payment.PaymentIds.Contains(p.Id) &&
                               ((p.OrderId.HasValue && _context.PlatformTransactions.Any(pt => pt.OrderId == p.OrderId && pt.Category == PlatformTransactionCategory.OrderPayment && !pt.IsDeleted)) ||
                                (p.AmbassadorSubscriptionId.HasValue && _context.PlatformTransactions.Any(pt => pt.AmbassadorSubscriptionId == p.AmbassadorSubscriptionId && pt.Category == PlatformTransactionCategory.SubscriptionPayment && !pt.IsDeleted))))
                    .CountAsync();

                var auditPercentage = payment.Count > 0 ? (decimal)auditedCount / payment.Count * 100 : 0;

                PaymentMethodSummary.Add(new PaymentMethodSummaryDto
                {
                    Method = payment.Method.ToString(),
                    Amount = payment.Amount,
                    AuditPercentage = auditPercentage
                });
            }
        }

        private async Task LoadTransactionDetailsAsync()
        {
            var query = _context.Payments
                .Include(p => p.User)
                .Where(p => p.Status == PaymentStatus.Completed &&
                           p.CreatedAt >= StartDate && p.CreatedAt <= EndDate);

            // Filtre audit : selon type de paiement (commande = OrderPayment, abo = SubscriptionPayment)
            if (AuditStatus == "audited")
            {
                query = query.Where(p =>
                    (p.OrderId.HasValue && _context.PlatformTransactions.Any(pt => pt.OrderId == p.OrderId && pt.Category == PlatformTransactionCategory.OrderPayment && !pt.IsDeleted)) ||
                    (p.AmbassadorSubscriptionId.HasValue && _context.PlatformTransactions.Any(pt => pt.AmbassadorSubscriptionId == p.AmbassadorSubscriptionId && pt.Category == PlatformTransactionCategory.SubscriptionPayment && !pt.IsDeleted)));
            }
            else if (AuditStatus == "unaudited")
            {
                query = query.Where(p =>
                    (p.OrderId.HasValue && !_context.PlatformTransactions.Any(pt => pt.OrderId == p.OrderId && pt.Category == PlatformTransactionCategory.OrderPayment && !pt.IsDeleted)) ||
                    (p.AmbassadorSubscriptionId.HasValue && !_context.PlatformTransactions.Any(pt => pt.AmbassadorSubscriptionId == p.AmbassadorSubscriptionId && pt.Category == PlatformTransactionCategory.SubscriptionPayment && !pt.IsDeleted)) ||
                    (!p.OrderId.HasValue && !p.AmbassadorSubscriptionId.HasValue));
            }

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(100) // Limiter pour les performances
                .ToListAsync();

            FilteredTransactions = new List<TransactionReconciliationDto>();

            foreach (var payment in payments)
            {
                var isAudited = payment.OrderId.HasValue
                    ? await _context.PlatformTransactions.AnyAsync(pt => pt.OrderId == payment.OrderId && pt.Category == PlatformTransactionCategory.OrderPayment && !pt.IsDeleted)
                    : payment.AmbassadorSubscriptionId.HasValue
                        ? await _context.PlatformTransactions.AnyAsync(pt => pt.AmbassadorSubscriptionId == payment.AmbassadorSubscriptionId && pt.Category == PlatformTransactionCategory.SubscriptionPayment && !pt.IsDeleted)
                        : false;

                FilteredTransactions.Add(new TransactionReconciliationDto
                {
                    Id = payment.Id,
                    CreatedAt = payment.CreatedAt,
                    UserEmail = payment.User?.Email ?? "Utilisateur supprimé",
                    Amount = payment.Amount,
                    PaymentMethod = payment.PaymentMethod.ToString(),
                    IsAudited = isAudited,
                    Description = GetPaymentDescription(payment)
                });
            }
        }

        private async Task CheckReconciliationIssuesAsync()
        {
            ReconciliationIssues.Clear();

            // 1. Paiements sans audit (revenu commande ou abonnement non enregistré en PT)
            var unauditedOrderPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.OrderId.HasValue &&
                           !_context.PlatformTransactions.Any(pt => pt.OrderId == p.OrderId &&
                                                                    pt.Category == PlatformTransactionCategory.OrderPayment &&
                                                                    !pt.IsDeleted))
                .CountAsync();
            var unauditedSubscriptionPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.AmbassadorSubscriptionId.HasValue &&
                           !_context.PlatformTransactions.Any(pt => pt.AmbassadorSubscriptionId == p.AmbassadorSubscriptionId &&
                                                                    pt.Category == PlatformTransactionCategory.SubscriptionPayment &&
                                                                    !pt.IsDeleted))
                .CountAsync();
            var unauditedPayments = unauditedOrderPayments + unauditedSubscriptionPayments;

            if (unauditedPayments > 0)
            {
                ReconciliationIssues.Add($"{unauditedPayments} paiement(s) confirmé(s) sans audit PlatformTransaction (revenu commande ou abonnement)");
            }

            // 2. Orphelins : revenus PT sans Payment confirmé correspondant (OrderPayment / SubscriptionPayment uniquement)
            var orphanOrderRevenue = await _context.PlatformTransactions
                .Where(pt => pt.Category == PlatformTransactionCategory.OrderPayment && pt.OrderId.HasValue && !pt.IsDeleted &&
                            !_context.Payments.Any(p => p.OrderId == pt.OrderId && p.Status == PaymentStatus.Completed))
                .CountAsync();
            var orphanSubscriptionRevenue = await _context.PlatformTransactions
                .Where(pt => pt.Category == PlatformTransactionCategory.SubscriptionPayment && pt.AmbassadorSubscriptionId.HasValue && !pt.IsDeleted &&
                            !_context.Payments.Any(p => p.AmbassadorSubscriptionId == pt.AmbassadorSubscriptionId && p.Status == PaymentStatus.Completed))
                .CountAsync();
            var orphanAudits = orphanOrderRevenue + orphanSubscriptionRevenue;

            if (orphanAudits > 0)
            {
                ReconciliationIssues.Add($"{orphanAudits} audit(s) revenu (OrderPayment/SubscriptionPayment) sans paiement confirmé correspondant");
            }

            // 3. Vérifier les montants incohérents (uniquement catégorie OrderPayment = revenu commande)
            var inconsistentAmounts = await _context.Payments
                .Where(p => p.OrderId.HasValue && p.Status == PaymentStatus.Completed &&
                            _context.PlatformTransactions.Any(pt => pt.OrderId == p.OrderId &&
                                                                     pt.Category == PlatformTransactionCategory.OrderPayment &&
                                                                     pt.Amount != p.Amount))
                .CountAsync();

            if (inconsistentAmounts > 0)
            {
                ReconciliationIssues.Add($"{inconsistentAmounts} incohérence(s) de montant entre Payment et PlatformTransaction (revenu commande)");
            }

            // 4. Vérifier la couverture d'audit
            if (AuditCoveragePercent < 95)
            {
                ReconciliationIssues.Add($"Couverture d'audit insuffisante: {AuditCoveragePercent:F1}% (objectif: 95%+)");
            }

            // 5. Cohérence solde plateforme = somme des PlatformTransaction
            if (!PlatformBalanceMatchesPT)
            {
                ReconciliationIssues.Add($"Solde wallet plateforme ({PlatformWalletBalance:C}) ne correspond pas à la somme des PlatformTransaction ({NetBalanceFromPT:C}). Vérifier les écritures.");
            }
        }

        private string GetPaymentDescription(Payment payment)
        {
            if (payment.OrderId.HasValue)
                return $"Commande n°{payment.OrderId}";
            
            if (payment.AmbassadorSubscriptionId.HasValue)
                return "Plan Ambassadeur";
            
            return $"Paiement {payment.PaymentMethod}";
        }
    }

    // DTOs pour l'affichage
    public class TransactionReconciliationDto
    {
        public int Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string UserEmail { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public bool IsAudited { get; set; }
        public string Description { get; set; }
    }

    public class PaymentMethodSummaryDto
    {
        public string Method { get; set; }
        public decimal Amount { get; set; }
        public decimal AuditPercentage { get; set; }
    }
}
