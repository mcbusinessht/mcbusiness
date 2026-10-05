using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Middlewares;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service dédié au calcul des commissions ambassadeur selon la nouvelle logique :
    /// - Commission par panier séparée par vendeur
    /// - Seuls les produits de l'entreprise liée au lien touchent une commission
    /// - Logique simple et robuste
    /// </summary>
    public class AmbassadorCommissionCalculator
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AmbassadorCommissionCalculator> _logger;

        public AmbassadorCommissionCalculator(
            AppDbContext context,
            ILogger<AmbassadorCommissionCalculator> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Calcule et crée les commissions ambassadeur pour une commande payée
        /// RÈGLE: Commission par panier, séparée par vendeur
        /// </summary>
        public async Task<List<AmbassadorCommission>> CalculateCommissionsForPaidOrderAsync(Order order, string ambassadorReferralCode, int? trackedCompanyId = null)
        {
            var commissions = new List<AmbassadorCommission>();
            
            try
            {
                // Vérifier que l'ambassadeur existe
                var ambassador = await _context.Users
                    .FirstOrDefaultAsync(u => u.ReferralCode == ambassadorReferralCode);

                if (ambassador == null)
                {
                    _logger.LogWarning($"Ambassador not found with code: {ambassadorReferralCode}");
                    return commissions;
                }

                // Récupérer le plan ambassadeur actif
                var ambassadorSubscription = await _context.AmbassadorSubscriptions
                    .Include(s => s.Plan)
                    .Where(s => s.UserId == ambassador.Id && 
                               s.Status == SubscriptionStatus.Active && 
                               s.EndDate > DateTimeOffset.UtcNow)
                    .FirstOrDefaultAsync();

                if (ambassadorSubscription?.Plan == null)
                {
                    _logger.LogWarning($"No active plan found for ambassador {ambassador.Id}");
                    return commissions;
                }

                // Charger les produits de la commande avec leurs entreprises
                var orderProducts = await _context.OrderProducts
                    .Include(op => op.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                            .ThenInclude(p => p.Company)
                    .Where(op => op.OrderId == order.Id)
                    .ToListAsync();

                if (!orderProducts.Any())
                {
                    _logger.LogWarning($"No products found for order {order.Id}");
                    return commissions;
                }

                // Grouper par entreprise pour créer une commission par vendeur
                var productsByCompany = orderProducts
                    .Where(op => op.ProductVariant?.Product?.Company != null)
                    .GroupBy(op => op.ProductVariant.Product.Company)
                    .ToList();

                foreach (var companyGroup in productsByCompany)
                {
                    var company = companyGroup.Key;
                    var companyProducts = companyGroup.ToList();

                    // RÈGLE PRINCIPALE: Commission SEULEMENT si cette entreprise correspond au lien tracké
                    bool shouldCalculateCommission = false;

                    if (trackedCompanyId.HasValue)
                    {
                        // Si un CompanyId était dans le lien, la commission s'applique UNIQUEMENT à cette entreprise
                        shouldCalculateCommission = (company.Id == trackedCompanyId.Value);
                    }
                    else
                    {
                        // Si pas de CompanyId spécifique, on peut appliquer une commission générale
                        // (logique optionnelle selon les besoins business)
                        shouldCalculateCommission = true; // À ajuster selon la politique
                    }

                    if (!shouldCalculateCommission)
                    {
                        _logger.LogDebug($"No commission calculated for company {company.Id} - not matching tracked company {trackedCompanyId}");
                        continue;
                    }

                    // Calculer le montant total pour cette entreprise
                    var companySubTotal = companyProducts.Sum(op => op.PriceAtOrder * op.Quantity);
                    
                    if (companySubTotal <= 0)
                    {
                        _logger.LogDebug($"No positive amount for company {company.Id} in order {order.Id}");
                        continue;
                    }

                    // Calculer le taux de commission selon le plan
                    var commissionRate = ambassadorSubscription.Plan.AmbassadorCommissionPercentage;

                    // Calculer le montant de commission
                    var commissionAmount = companySubTotal * commissionRate;

                    // Vérifier le plafond (ceiling) si défini
                    if (ambassadorSubscription.Plan.Ceiling > 0 && commissionAmount > ambassadorSubscription.Plan.Ceiling)
                    {
                        _logger.LogInformation($"Commission capped at ceiling: {ambassadorSubscription.Plan.Ceiling} for ambassador {ambassador.Id}");
                        commissionAmount = ambassadorSubscription.Plan.Ceiling;
                    }

                    // Créer la commission
                    var commission = new AmbassadorCommission
                    {
                        AmbassadorId = ambassador.Id,
                        OrderId = order.Id,
                        CompanyId = company.Id,
                        Amount = commissionAmount,
                        CommissionRate = commissionRate,
                        Type = AmbassadorCommissionType.DirectSale,
                        Status = AmbassadorCommissionStatus.Pending, // CORRIGÉ: Statut Pending pour respect de la garantie produit
                        AvailableAt = DateTime.UtcNow.AddDays(7), // AJOUTÉ: Délai de garantie de 7 jours
                        CurrencyId = order.CurrencyBase,
                        PlanNameSnapshot = ambassadorSubscription.Plan.Name,
                        ProcessingNotes = $"Commission calculée sur sous-total entreprise: {companySubTotal:F2}, taux: {commissionRate:P2}",
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    commissions.Add(commission);
                    _logger.LogInformation($"Commission created: {commissionAmount:F2} for ambassador {ambassador.Id}, company {company.Id}, order {order.Id}");
                }

                return commissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating commissions for order {order.Id}");
                return new List<AmbassadorCommission>();
            }
        }

        /// <summary>
        /// Enregistre les commissions calculées dans la base de données
        /// </summary>
        public async Task<bool> SaveCommissionsAsync(List<AmbassadorCommission> commissions)
        {
            try
            {
                if (!commissions.Any())
                {
                    _logger.LogDebug("No commissions to save");
                    return true;
                }

                // Vérifier les doublons avant insertion
                foreach (var commission in commissions)
                {
                    var existingCommission = await _context.AmbassadorCommissions
                        .FirstOrDefaultAsync(c => 
                            c.AmbassadorId == commission.AmbassadorId &&
                            c.OrderId == commission.OrderId &&
                            c.CompanyId == commission.CompanyId &&
                            c.Type == commission.Type);

                    if (existingCommission != null)
                    {
                        _logger.LogWarning($"Duplicate commission detected - skipping: Ambassador {commission.AmbassadorId}, Order {commission.OrderId}, Company {commission.CompanyId}");
                        continue;
                    }

                    _context.AmbassadorCommissions.Add(commission);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully saved {commissions.Count} commission(s)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ambassador commissions");
                return false;
            }
        }

        /// <summary>
        /// Méthode combinée : calcule et sauvegarde les commissions
        /// </summary>
        public async Task<bool> ProcessCommissionsForOrderAsync(Order order, HttpContext httpContext)
        {
            try
            {
                // Récupérer les informations de tracking
                var ambassadorCode = AmbassadorTrackingHelper.GetAmbassadorReferralCode(httpContext);
                var trackedCompanyId = AmbassadorTrackingHelper.GetAmbassadorCompanyId(httpContext);

                if (string.IsNullOrEmpty(ambassadorCode))
                {
                    _logger.LogDebug($"No ambassador tracking found for order {order.Id}");
                    return true; // Pas d'erreur, juste pas de commission
                }

                // Calculer les commissions
                var commissions = await CalculateCommissionsForPaidOrderAsync(order, ambassadorCode, trackedCompanyId);

                // Sauvegarder
                return await SaveCommissionsAsync(commissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing commissions for order {order.Id}");
                return false;
            }
        }
    }
}
