using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Middlewares;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service d'intégration pour lier les commandes aux commissions ambassadeur
    /// S'intègre harmonieusement dans le flux de commande MCBusiness existant
    /// </summary>
    public class OrderAmbassadorIntegrationService
    {
        private readonly AppDbContext _context;
        private readonly AmbassadorCommissionService _commissionService;
        private readonly IAmbassadorAccessService _ambassadorAccess;
        private readonly ILogger<OrderAmbassadorIntegrationService> _logger;

        public OrderAmbassadorIntegrationService(
            AppDbContext context,
            AmbassadorCommissionService commissionService,
            IAmbassadorAccessService ambassadorAccess,
            ILogger<OrderAmbassadorIntegrationService> logger)
        {
            _context = context;
            _commissionService = commissionService;
            _ambassadorAccess = ambassadorAccess;
            _logger = logger;
        }

        /// <summary>
        /// Associe une commande à un ambassadeur au moment de la création
        /// À appeler depuis le processus de commande existant
        /// </summary>
        public async Task<bool> AssociateOrderWithAmbassadorAsync(Order order, HttpContext httpContext)
        {
            try
            {
                // Récupérer le code de référence ambassadeur
                var referralCode = AmbassadorTrackingHelper.GetAmbassadorReferralCode(httpContext);
                var referralId = AmbassadorTrackingHelper.GetAmbassadorReferralId(httpContext);
                
                // [DIAG-TEMP] Log lecture cookies
                _logger.LogWarning("[DIAG-TEMP] ORDER-INTEGRATION: Lecture tracking - ref={RefCode}, ambassadorId={AmbassadorId}, orderId={OrderId}", 
                    referralCode ?? "NULL", referralId ?? "NULL", order.Id);
                
                if (string.IsNullOrEmpty(referralCode) && string.IsNullOrEmpty(referralId))
                {
                    _logger.LogDebug($"No ambassador referral found for order {order.Id}");
                    return false;
                }

                // Vérifier que la référence est encore active (7 jours max)
                if (!AmbassadorTrackingHelper.IsAmbassadorReferralActive(httpContext, maxDaysOld: 7))
                {
                    _logger.LogDebug($"Ambassador referral expired for order {order.Id}");
                    return false;
                }

                // Vérifier l'ambassadeur
                ApplicationUser? ambassador = null;
                if (!string.IsNullOrEmpty(referralId))
                {
                    ambassador = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == referralId);
                }

                if (ambassador == null && !string.IsNullOrEmpty(referralCode))
                {
                    ambassador = await _context.Users
                        .FirstOrDefaultAsync(u => u.ReferralCode != null && u.ReferralCode.ToUpper() == referralCode.ToUpper());
                }

                if (ambassador == null)
                {
                    _logger.LogWarning("Ambassador not found for order {OrderId}. RefCode={RefCode}, RefId={RefId}", 
                        order.Id, referralCode ?? "NULL", referralId ?? "NULL");
                    return false;
                }

                // Vérification anti-fraude : pas d'auto-référence
                if (ambassador.Id == order.UserId)
                {
                    _logger.LogWarning($"Self-referral blocked - Ambassador {ambassador.Id} tried to refer themselves");
                    AmbassadorTrackingHelper.ClearAmbassadorReferral(httpContext);
                    return false;
                }

                // Vérifier que l'ambassadeur a un abonnement actif
                var hasActiveSubscription = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(ambassador.Id);
                if (!hasActiveSubscription)
                {
                    _logger.LogWarning($"Ambassador {ambassador.Id} has no active subscription");
                    return false;
                }

                // Associer la commande à l'ambassadeur
                order.ReferredByAmbassador = ambassador.Id;
                
                // Sauvegarder
                await _context.SaveChangesAsync();

                // [DIAG-TEMP] Confirmation attribution
                _logger.LogWarning("[DIAG-TEMP] ORDER-INTEGRATION: Attribution réussie - orderId={OrderId}, ref={RefCode}, ambassadorId={AmbassadorId}", 
                    order.Id, referralCode, ambassador.Id);

                _logger.LogInformation($"Order {order.Id} associated with ambassador {ambassador.Id} (code: {referralCode})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error associating order {order.Id} with ambassador");
                return false;
            }
        }

        /// <summary>
        /// Traite les commissions lorsqu'une commande passe au statut DELIVERED
        /// À intégrer dans OrderStatusService ou équivalent
        /// </summary>
        public async Task<bool> ProcessCommissionsForDeliveredOrderAsync(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.ReferringAmbassador)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found for commission processing");
                    return false;
                }

                // Vérifier si c'est une commande avec ambassadeur
                if (string.IsNullOrEmpty(order.ReferredByAmbassador))
                {
                    _logger.LogDebug($"Order {orderId} has no ambassador referral");
                    return false;
                }

                // PROTECTION IDEMPOTENCE: Vérifier si les commissions ont déjà été créées
                var existingCommissions = await MCBusiness.Helpers.IdempotencyHelper.CommissionExistsForOrderAsync(_context, orderId);

                if (existingCommissions)
                {
                    _logger.LogDebug($"Commissions already exist for order {orderId}");
                    return false;
                }

                // Vérifier le statut de la commande
                if (order.Status != OrderStatus.Delivered)
                {
                    _logger.LogDebug($"Order {orderId} is not delivered yet (status: {order.Status})");
                    return false;
                }

                // Calculer et créer les commissions
                var commissions = await _commissionService.CalculateCommissionsAsync(order);
                
                if (!commissions.Any())
                {
                    _logger.LogWarning($"No commissions calculated for order {orderId}");
                    return false;
                }

                // Ajouter les commissions à la base de données
                foreach (var commission in commissions)
                {
                    _context.AmbassadorCommissions.Add(commission);
                    _logger.LogDebug($"Commission created: {commission.Amount} HTG for ambassador {commission.AmbassadorId}");
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully processed {commissions.Count} commissions for order {orderId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing commissions for delivered order {orderId}");
                return false;
            }
        }

        /// <summary>
        /// Annule les commissions si une commande est annulée ou remboursée
        /// </summary>
        public async Task<bool> CancelCommissionsForOrderAsync(int orderId, string reason = "Order cancelled")
        {
            try
            {
                var commissions = await _context.AmbassadorCommissions
                    .Where(c => c.OrderId == orderId)
                    .ToListAsync();

                if (!commissions.Any())
                {
                    _logger.LogDebug($"No commissions found for order {orderId} to cancel");
                    return true;
                }

                foreach (var commission in commissions)
                {
                    // Seules les commissions non encore payées peuvent être annulées
                    if (commission.Status != AmbassadorCommissionStatus.Paid)
                    {
                        commission.Status = AmbassadorCommissionStatus.Cancelled;
                        commission.RejectionReason = reason;
                        
                        _logger.LogDebug($"Commission {commission.Id} cancelled for order {orderId}");
                    }
                    else
                    {
                        _logger.LogWarning($"Cannot cancel paid commission {commission.Id} for order {orderId}");
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Cancelled {commissions.Count} commissions for order {orderId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling commissions for order {orderId}");
                return false;
            }
        }

        /// <summary>
        /// Méthode helper pour les contrôleurs de commande
        /// </summary>
        public async Task<string?> GetCurrentAmbassadorReferralAsync(HttpContext httpContext)
        {
            var referralCode = AmbassadorTrackingHelper.GetAmbassadorReferralCode(httpContext);
            
            if (string.IsNullOrEmpty(referralCode))
                return null;

            // Vérifier que l'ambassadeur existe et est actif
            var ambassador = await _context.Users
                .FirstOrDefaultAsync(u => u.ReferralCode == referralCode);

            if (ambassador == null)
                return null;

            var hasActiveSubscription = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(ambassador.Id);
            if (!hasActiveSubscription)
                return null;

            return referralCode;
        }

        /// <summary>
        /// Statistiques pour l'administration
        /// </summary>
        public async Task<AmbassadorOrderStats> GetAmbassadorOrderStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var query = _context.Orders.AsQueryable();

                if (fromDate.HasValue)
                    query = query.Where(o => o.CreatedAt >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(o => o.CreatedAt <= toDate.Value);

                var stats = new AmbassadorOrderStats
                {
                    TotalOrdersWithAmbassador = await query.CountAsync(o => !string.IsNullOrEmpty(o.ReferredByAmbassador)),
                    TotalOrdersWithoutAmbassador = await query.CountAsync(o => string.IsNullOrEmpty(o.ReferredByAmbassador)),
                    TotalCommissionsPending = await _context.AmbassadorCommissions
                        .Where(c => c.Status == AmbassadorCommissionStatus.Pending
                            || c.Status == AmbassadorCommissionStatus.Available
                            || c.Status == AmbassadorCommissionStatus.Approved)
                        .SumAsync(c => c.Amount),
                    TotalCommissionsPaid = await _context.AmbassadorCommissions
                        .Where(c => c.Status == AmbassadorCommissionStatus.Paid)
                        .SumAsync(c => c.Amount),
                    ActiveAmbassadors = await _context.Users
                        .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && 
                            _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Ambassador")))
                        .CountAsync()
                };

                stats.TotalOrders = stats.TotalOrdersWithAmbassador + stats.TotalOrdersWithoutAmbassador;
                stats.AmbassadorConversionRate = stats.TotalOrders > 0 
                    ? (double)stats.TotalOrdersWithAmbassador / stats.TotalOrders * 100 
                    : 0;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating ambassador order stats");
                return new AmbassadorOrderStats();
            }
        }
    }

    /// <summary>
    /// Statistiques des commandes ambassadeur
    /// </summary>
    public class AmbassadorOrderStats
    {
        public int TotalOrders { get; set; }
        public int TotalOrdersWithAmbassador { get; set; }
        public int TotalOrdersWithoutAmbassador { get; set; }
        public decimal TotalCommissionsPending { get; set; }
        public decimal TotalCommissionsPaid { get; set; }
        public int ActiveAmbassadors { get; set; }
        public double AmbassadorConversionRate { get; set; }
    }
}
