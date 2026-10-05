using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using System.Data;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service dédié à la gestion complète des commissions ambassadeur
    /// Logique métier autonome et indépendante
    /// </summary>
    public class AmbassadorCommissionService
    {
        private readonly AppDbContext _context;
        private readonly IAmbassadorAccessService _ambassadorAccess;
        private readonly IPlatformTransactionAuditService _auditService;
        private readonly ILogger<AmbassadorCommissionService> _logger;

        public AmbassadorCommissionService(
            AppDbContext context,
            IAmbassadorAccessService ambassadorAccess,
            IPlatformTransactionAuditService auditService,
            ILogger<AmbassadorCommissionService> logger)
        {
            _context = context;
            _ambassadorAccess = ambassadorAccess;
            _auditService = auditService;
            _logger = logger;
        }

        #region CALCUL COMMISSIONS

        /// <summary>
        /// Calcule toutes les commissions pour une commande donnée
        /// POINT D'ENTRÉE PRINCIPAL
        /// </summary>
        public async Task<List<AmbassadorCommission>> CalculateCommissionsAsync(Order order)
        {
            var commissions = new List<AmbassadorCommission>();
            
            // [DIAG-TEMP] Log début calcul
            _logger.LogWarning("[DIAG-TEMP] COMMISSION-SERVICE: Début calcul - orderId={OrderId}, ref={RefCode}, status={Status}", 
                order.Id, order.ReferredByAmbassador ?? "NULL", order.Status);
            
            if (string.IsNullOrEmpty(order.ReferredByAmbassador))
                return commissions;

            try
            {
                // 1. Commission directe (PRIORITÉ 1)
                var directCommissions = await CalculateDirectCommissionsByCompanyAsync(order);
                if (directCommissions.Any())
                {
                    commissions.AddRange(directCommissions);
                    // [DIAG-TEMP] Commission créée
                    _logger.LogWarning("[DIAG-TEMP] COMMISSION-SERVICE: Commission créée - orderId={OrderId}, ambassadorId={AmbassadorId}, amount={Amount}", 
                        order.Id, directCommissions.First().AmbassadorId, directCommissions.Sum(c => c.Amount));
                }
                else
                {
                    // [DIAG-TEMP] Pas de commission
                    _logger.LogWarning("[DIAG-TEMP] COMMISSION-SERVICE: Aucune commission créée pour orderId={OrderId}", order.Id);
                }

                // 2. Bonus parrainage générations 2-3 (PRIORITÉ 2)
                var referralCommissions = await CalculateReferralCommissionsAsync(order);
                commissions.AddRange(referralCommissions);

                _logger.LogInformation($"Calculated {commissions.Count} commissions for order {order.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating commissions for order {order.Id}");
            }

            return commissions;
        }

        /// <summary>
        /// Commission directe sur vente (PRINCIPAL)
        /// </summary>
        private async Task<List<AmbassadorCommission>> CalculateDirectCommissionsByCompanyAsync(Order order)
        {
            var commissions = new List<AmbassadorCommission>();

            var ambassador = await GetAmbassadorByIdAsync(order.ReferredByAmbassador);
            if (ambassador == null)
            {
                _logger.LogWarning(
                    "No ambassador found for id {AmbassadorId} on order {OrderId}",
                    order.ReferredByAmbassador,
                    order.Id);
                return commissions;
            }

            var subscription = await _ambassadorAccess.GetActiveSubscriptionAsync(ambassador.Id);
            if (subscription?.Plan == null)
            {
                _logger.LogWarning(
                    "Ambassador {AmbassadorId} has no active subscription plan for order {OrderId}",
                    ambassador.Id,
                    order.Id);
                return commissions;
            }

            if (!await IsCommissionEligibleAsync(ambassador, order))
            {
                return commissions;
            }

            var commissionRate = subscription.Plan.AmbassadorCommissionPercentage;
            if (commissionRate <= 0)
            {
                _logger.LogWarning("Commission rate is zero for ambassador {AmbassadorId}, order {OrderId}", ambassador.Id, order.Id);
                return commissions;
            }

            var remainingCeiling = await GetRemainingCeilingAsync(ambassador.Id, subscription.Plan);
            if (subscription.Plan.Ceiling > 0 && remainingCeiling <= 0)
            {
                _logger.LogWarning("Monthly commission ceiling reached for ambassador {AmbassadorId}, order {OrderId}", ambassador.Id, order.Id);
                return commissions;
            }

            var productsByCompany = await _context.OrderProducts
                .AsNoTracking()
                .Where(op => op.OrderId == order.Id
                    && !op.IsDeleted
                    && op.Status != OrderProductStatus.Cancelled
                    && op.Status != OrderProductStatus.Returned)
                .GroupBy(op => op.CompanyId)
                .Select(g => new
                {
                    CompanyId = g.Key,
                    ProductTotal = g.Sum(op => op.SubTotal)
                })
                .ToListAsync();

            foreach (var companyGroup in productsByCompany)
            {
                if (companyGroup.ProductTotal <= 0)
                {
                    continue;
                }

                var baseAmount = companyGroup.ProductTotal * commissionRate;
                var finalAmount = subscription.Plan.Ceiling > 0
                    ? Math.Min(baseAmount, remainingCeiling)
                    : baseAmount;

                if (finalAmount <= 0)
                {
                    break;
                }

                commissions.Add(new AmbassadorCommission
                {
                    AmbassadorId = ambassador.Id,
                    OrderId = order.Id,
                    CompanyId = companyGroup.CompanyId,
                    Amount = finalAmount,
                    CommissionRate = commissionRate,
                    Type = AmbassadorCommissionType.DirectSale,
                    Status = AmbassadorCommissionStatus.Pending,
                    AvailableAt = DateTime.UtcNow.AddDays(7),
                    CurrencyId = order.CurrencyBase,
                    PlanNameSnapshot = subscription.Plan.Name,
                    ProcessingNotes = $"Commission directe - vendeur #{companyGroup.CompanyId}, base produits: {companyGroup.ProductTotal:F2}, taux: {commissionRate:P2}"
                });

                if (subscription.Plan.Ceiling > 0)
                {
                    remainingCeiling -= finalAmount;
                }
            }

            return commissions;
        }

        private async Task<AmbassadorCommission?> CalculateDirectCommissionAsync(Order order)
        {
            var ambassador = await GetAmbassadorByIdAsync(order.ReferredByAmbassador);
            if (ambassador == null)
            {
                _logger.LogWarning(
                    "No ambassador found for id {AmbassadorId} on order {OrderId}",
                    order.ReferredByAmbassador,
                    order.Id);
                return null;
            }

            var subscription = await _ambassadorAccess.GetActiveSubscriptionAsync(ambassador.Id);
            if (subscription?.Plan == null)
            {
                _logger.LogWarning(
                    "Ambassador {AmbassadorId} has no active subscription plan for order {OrderId}",
                    ambassador.Id,
                    order.Id);
                return null;
            }

            // VÉRIFICATIONS ANTI-FRAUDE
            if (!await IsCommissionEligibleAsync(ambassador, order)) return null;

            // CALCUL AVEC PLAFOND
            var companyId = await GetOrderCompanyIdAsync(order);
            var productTotal = await _context.OrderProducts
                .AsNoTracking()
                .Where(op => op.OrderId == order.Id
                    && op.CompanyId == companyId
                    && !op.IsDeleted
                    && op.Status != OrderProductStatus.Cancelled
                    && op.Status != OrderProductStatus.Returned)
                .SumAsync(op => op.SubTotal);
            var baseAmount = productTotal * subscription.Plan.AmbassadorCommissionPercentage;
            var finalAmount = await ApplyCeilingLimitAsync(ambassador.Id, baseAmount, subscription.Plan);

            if (finalAmount <= 0)
            {
                _logger.LogWarning(
                    "Commission amount is zero after ceiling check for ambassador {AmbassadorId}, order {OrderId}, base amount {BaseAmount}",
                    ambassador.Id,
                    order.Id,
                    baseAmount);
                return null;
            }

            return new AmbassadorCommission
            {
                AmbassadorId = ambassador.Id,
                OrderId = order.Id,
                CompanyId = companyId,
                Amount = finalAmount,
                CommissionRate = subscription.Plan.AmbassadorCommissionPercentage,
                Type = AmbassadorCommissionType.DirectSale,
                // SUPPRIMÉ: Generation
                Status = AmbassadorCommissionStatus.Pending, // CORRIGÉ: Pending au lieu d'Approved
                AvailableAt = DateTime.UtcNow.AddDays(7), // Délai de garantie produit
                CurrencyId = order.CurrencyBase,
                PlanNameSnapshot = subscription.Plan.Name,
                // ProductPriceSnapshot supprimé - redondant avec OrderProduct
                ProcessingNotes = $"Commission directe - Plan {subscription.Plan.Name}"
            };
        }

        /// <summary>
        /// COOKIES-BASED: Pas de commissions multi-génération pour les ambassadeurs
        /// Le système ambassadeur utilise uniquement cookies, donc une seule génération
        /// </summary>
        private async Task<List<AmbassadorCommission>> CalculateReferralCommissionsAsync(Order order)
        {
            // DÉSACTIVÉ pour système ambassadeur cookies-only
            // Les générations multiples nécessiteraient la table Referrals
            return new List<AmbassadorCommission>();
        }

        /// <summary>
        /// Commission pour une génération spécifique
        /// </summary>
        private async Task<AmbassadorCommission?> CalculateGenerationCommissionAsync(
            Order order, ApplicationUser ambassador, int generation)
        {
            var subscription = await _ambassadorAccess.GetActiveSubscriptionAsync(ambassador.Id);
            if (subscription?.Plan == null) return null;

            // Taux dégressif par génération
            var rate = generation switch
            {
                // SIMPLIFIÉ: Une seule commission, plus de multi-génération
                2 => subscription.Plan.AmbassadorCommissionPercentage,
                3 => subscription.Plan.AmbassadorCommissionPercentage,
                _ => 0
            };

            if (rate <= 0) return null;

            var baseAmount = order.TotalAmount * rate;
            var finalAmount = await ApplyCeilingLimitAsync(ambassador.Id, baseAmount, subscription.Plan);

            if (finalAmount <= 0) return null;

            return new AmbassadorCommission
            {
                AmbassadorId = ambassador.Id,
                OrderId = order.Id,
                CompanyId = await GetOrderCompanyIdAsync(order),
                Amount = finalAmount,
                CommissionRate = rate,
                Type = AmbassadorCommissionType.ReferralBonus,
                // SUPPRIMÉ: Generation
                Status = AmbassadorCommissionStatus.Pending, // CORRIGÉ: Pending au lieu d'Approved
                AvailableAt = DateTime.UtcNow.AddDays(7),
                CurrencyId = order.CurrencyBase,
                PlanNameSnapshot = subscription.Plan.Name,
                // ProductPriceSnapshot supprimé - redondant avec OrderProduct
                ProcessingNotes = $"Bonus génération {generation}"
            };
        }

        #endregion

        #region GESTION STATUTS

        /// <summary>
        /// Traite les commissions en attente de libération (après délai de garantie)
        /// </summary>
        public async Task ProcessPendingCommissionsAsync()
        {
            var pendingCommissions = await _context.AmbassadorCommissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Pending
                         && c.AvailableAt.HasValue
                         && c.AvailableAt.Value <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var commission in pendingCommissions)
            {
                commission.Status = AmbassadorCommissionStatus.Available;
                commission.ApprovedAt = DateTime.UtcNow;
            }

            if (pendingCommissions.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Marked {pendingCommissions.Count} commissions as available");
            }
        }

        public async Task<bool> PayAvailableCommissionAsync(int commissionId)
        {
            var commission = await _context.AmbassadorCommissions
                .FirstOrDefaultAsync(c => c.Id == commissionId && !c.IsDeleted);

            if (commission == null)
            {
                _logger.LogWarning("Commission {CommissionId} not found for payment", commissionId);
                return false;
            }

            if (!commission.CanBePaid)
            {
                _logger.LogWarning(
                    "Commission {CommissionId} cannot be paid from status {Status}",
                    commission.Id,
                    commission.Status);
                return false;
            }

            await ReleaseCommissionAsync(commission);
            return commission.Status == AmbassadorCommissionStatus.Paid;
        }

        /// <summary>
        /// Libère une commission et crédite le wallet
        /// </summary>
        private async Task ReleaseCommissionAsync(AmbassadorCommission commission)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                    var wallet = await _context.Wallets
                        .FirstOrDefaultAsync(w => w.UserId == commission.AmbassadorId && w.CurrencyBase == commission.CurrencyId && w.Type == WalletType.Earnings);
                    if (wallet == null)
                    {
                        wallet = new Wallet
                        {
                            UserId = commission.AmbassadorId,
                            Type = WalletType.Earnings,
                            CurrencyBase = commission.CurrencyId,
                            Balance = 0m,
                            CreatedAt = DateTimeOffset.UtcNow,
                            LastUpdated = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                        _context.Wallets.Add(wallet);
                        await _context.SaveChangesAsync();
                    }

                    await _auditService.RecordAmbassadorCommissionAsync(
                        commission.OrderId,
                        commission.AmbassadorId,
                        commission.Amount,
                        $"Paiement commission {commission.Type} - Commission #{commission.Id}",
                        commission.Id);

                    wallet.Balance += commission.Amount;
                    wallet.LastUpdated = DateTimeOffset.UtcNow;
                    wallet.UpdatedAt = DateTimeOffset.UtcNow;

                    var walletTransaction = new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        UserId = commission.AmbassadorId,
                        Amount = commission.Amount,
                        Balance = wallet.Balance,
                        WalletType = WalletType.Earnings,
                        Type = commission.Type == AmbassadorCommissionType.DirectSale
                            ? WalletTransactionType.CommissionSale
                            : WalletTransactionType.CommissionReferral,
                        Description = $"Commission commande #{commission.OrderId} - {commission.Type}",
                        CurrencyBase = commission.CurrencyId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.WalletTransactions.Add(walletTransaction);

                    commission.Status = AmbassadorCommissionStatus.Paid;
                    commission.ApprovedAt = DateTime.UtcNow;
                    commission.PaidAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    commission.WalletTransactionId = walletTransaction.Id;
                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();
                });

                _logger.LogInformation($"Released commission {commission.Id} - Amount: {commission.Amount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error releasing commission {commission.Id}");
                commission.Status = AmbassadorCommissionStatus.Rejected;
                commission.RejectionReason = "Erreur lors de la liberation";
            }
        }
        #endregion

        #region CONTRÃ”LES ET VALIDATIONS

        /// <summary>
        /// Vérifications anti-fraude complètes
        /// </summary>
        private async Task<bool> IsCommissionEligibleAsync(ApplicationUser ambassador, Order order)
        {
            // 1. Pas d'auto-référence
            if (ambassador.Id == order.UserId)
            {
                _logger.LogWarning($"Self-referral blocked - Ambassador: {ambassador.Id}, Order: {order.Id}");
                return false;
            }

            // 2. Commission pas déjà créée
            var existingCommission = await _context.AmbassadorCommissions
                .AnyAsync(c => c.OrderId == order.Id && c.AmbassadorId == ambassador.Id);
            
            if (existingCommission)
            {
                _logger.LogWarning($"Duplicate commission blocked - Ambassador: {ambassador.Id}, Order: {order.Id}");
                return false;
            }

            // 3. Commande livrée et valide
            if (order.Status != OrderStatus.Delivered)
            {
                _logger.LogWarning($"Order not delivered - Order: {order.Id}");
                return false;
            }

            // 4. Ambassadeur actif
            if (!await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(ambassador.Id))
            {
                _logger.LogWarning($"Ambassador inactive - Ambassador: {ambassador.Id}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Applique le plafond mensuel
        /// </summary>
        private async Task<decimal> ApplyCeilingLimitAsync(string ambassadorId, decimal amount, AmbassadorPlan plan)
        {
            if (plan.Ceiling <= 0)
            {
                return amount;
            }

            var remaining = await GetRemainingCeilingAsync(ambassadorId, plan);
            return Math.Min(amount, Math.Max(0, remaining));
        }

        private async Task<decimal> GetRemainingCeilingAsync(string ambassadorId, AmbassadorPlan plan)
        {
            if (plan.Ceiling <= 0)
            {
                return decimal.MaxValue;
            }

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;

            var monthlyTotal = await _context.AmbassadorCommissions
                .Where(c => c.AmbassadorId == ambassadorId
                         && c.CreatedAt.Month == currentMonth
                         && c.CreatedAt.Year == currentYear
                         && (c.Status == AmbassadorCommissionStatus.Pending 
                          || c.Status == AmbassadorCommissionStatus.Available
                          || c.Status == AmbassadorCommissionStatus.Paid))
                .SumAsync(c => c.Amount);

            return Math.Max(0, plan.Ceiling - monthlyTotal);
        }

        #endregion

        #region UTILITAIRES

        private async Task<ApplicationUser?> GetAmbassadorByCodeAsync(string referralCode)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.ReferralCode == referralCode);
        }

        private async Task<ApplicationUser?> GetAmbassadorByIdAsync(string ambassadorId)
        {
            if (string.IsNullOrEmpty(ambassadorId))
            {
                return null;
            }

            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == ambassadorId);
        }

        // SUPPRIMÉ: GetReferrerAsync - Plus utilisé dans système ambassadeur cookies-only
        // L'ancien système de parrainage (Referrals table) reste disponible pour autres usages

        private async Task<int> GetOrderCompanyIdAsync(Order order)
        {
            // Logique pour déterminer l'entreprise de la commande
            // Basée sur les produits de la commande
            var firstOrderProduct = await _context.OrderProducts
                .AsNoTracking()
                .Where(op => op.OrderId == order.Id
                    && !op.IsDeleted
                    && op.Status != OrderProductStatus.Cancelled
                    && op.Status != OrderProductStatus.Returned)
                .OrderBy(op => op.Id)
                .FirstOrDefaultAsync();

            return firstOrderProduct?.CompanyId ?? 1;
        }

        #endregion

        #region RAPPORTS ET STATISTIQUES

        /// <summary>
        /// Statistiques pour le dashboard ambassadeur
        /// </summary>
        public async Task<AmbassadorCommissionStats> GetCommissionStatsAsync(string ambassadorId)
        {
            var thisMonth = DateTime.UtcNow.Month;
            var thisYear = DateTime.UtcNow.Year;

            var stats = new AmbassadorCommissionStats
            {
                ThisMonthCommissions = await _context.AmbassadorCommissions
                    .Where(c => c.AmbassadorId == ambassadorId
                             && c.CreatedAt.Month == thisMonth
                             && c.CreatedAt.Year == thisYear
                             && c.Status != AmbassadorCommissionStatus.Rejected)
                    .SumAsync(c => c.Amount),

                TotalCommissions = await _context.AmbassadorCommissions
                    .Where(c => c.AmbassadorId == ambassadorId
                             && c.Status != AmbassadorCommissionStatus.Rejected)
                    .SumAsync(c => c.Amount),

                PendingCommissions = await _context.AmbassadorCommissions
                    .Where(c => c.AmbassadorId == ambassadorId
                             && c.Status == AmbassadorCommissionStatus.Pending)
                    .SumAsync(c => c.Amount),

                TotalSaleCommissions = await _context.AmbassadorCommissions
                    .Where(c => c.AmbassadorId == ambassadorId
                             && c.Type == AmbassadorCommissionType.DirectSale
                             && c.Status != AmbassadorCommissionStatus.Rejected)
                    .SumAsync(c => c.Amount),

                TotalReferralCommissions = await _context.AmbassadorCommissions
                    .Where(c => c.AmbassadorId == ambassadorId
                             && c.Type == AmbassadorCommissionType.ReferralBonus
                             && c.Status != AmbassadorCommissionStatus.Rejected)
                    .SumAsync(c => c.Amount)
            };

            return stats;
        }

        #endregion
    }

    /// <summary>
    /// Statistiques des commissions ambassadeur
    /// </summary>
    public class AmbassadorCommissionStats
    {
        public decimal ThisMonthCommissions { get; set; }
        public decimal TotalCommissions { get; set; }
        public decimal PendingCommissions { get; set; }
        public decimal TotalSaleCommissions { get; set; }
        public decimal TotalReferralCommissions { get; set; }
    }
}
