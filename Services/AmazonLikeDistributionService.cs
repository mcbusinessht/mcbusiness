using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service de répartition financière selon le modèle "Amazon-like"
    /// RÈGLE : Commission vendeur fixe, ambassadeur payé par plateforme, promo financé par plateforme
    /// </summary>
    public interface IAmazonLikeDistributionService
    {
        Task<DistributionResult> CalculateDistributionAsync(int orderId, int companyId);
    }

    public class AmazonLikeDistributionService : IAmazonLikeDistributionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AmazonLikeDistributionService> _logger;

        public AmazonLikeDistributionService(AppDbContext context, ILogger<AmazonLikeDistributionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Calcule la répartition financière complète selon le modèle Amazon-like
        /// </summary>
        public async Task<DistributionResult> CalculateDistributionAsync(int orderId, int companyId)
        {
            try
            {
                // 1. Récupérer la commande complète
                var order = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.ReferringAmbassador)
                    .Include(o => o.AmbassadorCommissions)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found");
                    return DistributionResult.Failed("Commande introuvable");
                }

                var completedPayment = await _context.Payments
                    .AsNoTracking()
                    .Include(p => p.PromoCodeUsage)
                    .Where(p => p.OrderId == orderId
                        && p.Status == PaymentStatus.Completed
                        && !p.IsDeleted)
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();

                if (completedPayment != null)
                {
                    order.Payment = completedPayment;
                }

                // 2. Récupérer les produits de ce vendeur
                var orderProducts = await _context.OrderProducts
                    .AsNoTracking()
                    .Include(op => op.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                    .Where(op => op.OrderId == orderId
                        && op.CompanyId == companyId
                        && !op.IsDeleted
                        && op.Status != OrderProductStatus.Cancelled
                        && op.Status != OrderProductStatus.Returned)
                    .ToListAsync();

                if (!orderProducts.Any())
                {
                    _logger.LogWarning($"No products found for order {orderId} and company {companyId}");
                    return DistributionResult.Failed("Aucun produit trouvé");
                }

                // 3. CALCUL SELON MODÈLE AMAZON-LIKE
                var result = await CalculateAmazonLikeDistribution(order, orderProducts, companyId);

                _logger.LogInformation(
                    $"Distribution calculated for Order {orderId}, Company {companyId}: " +
                    $"Seller={result.SellerAmount}, Platform={result.PlatformProfit}, Ambassador={result.AmbassadorCommission}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating distribution for order {orderId}, company {companyId}");
                return DistributionResult.Failed($"Erreur: {ex.Message}");
            }
        }

        private async Task<DistributionResult> CalculateAmazonLikeDistribution(
            Order order, 
            List<OrderProduct> orderProducts, 
            int companyId)
        {
            var result = new DistributionResult { Success = true };

            // ÉTAPE 1: Montant des produits (prix original affiché)
            result.ProductTotal = orderProducts.Sum(op => op.SubTotal);
            result.DeliveryFee = orderProducts.Sum(op => op.DeliveryFee);

            // ÉTAPE 2: Commission vendeur FIXE (par défaut 10%, configurable)
            var SellerCommissionRate = await GetSupplierCommissionRateAsync();
            result.SellerCommissionRate = SellerCommissionRate;
            
            // ÉTAPE 3: PromoCode (coût marketing plateforme)
            if (order.Payment?.PromoCodeUsage != null)
            {
                result.PromoDiscountAmount = order.Payment.PromoCodeUsage.DiscountApplied;
                result.HasPromoCode = true;
            }

            // ÉTAPE 4: Montant réellement payé par le client
            result.ActualPaidAmount = order.Payment?.Amount ?? order.TotalAmount;

            // ÉTAPE 5: Commission ambassadeur (si applicable)
            if (!string.IsNullOrEmpty(order.ReferredByAmbassador))
            {
                var ambassadorCommission = await CalculateAmbassadorCommissionAsync(order, companyId);
                if (ambassadorCommission != null)
                {
                    result.AmbassadorCommission = ambassadorCommission.Amount;
                    result.AmbassadorId = ambassadorCommission.AmbassadorId;
                    result.AmbassadorRate = ambassadorCommission.CommissionRate;
                    result.HasAmbassador = true;
                }
            }

            // ÉTAPE 6: FORMULE AMAZON-LIKE — Commission sur brut (ProductTotal + DeliveryFee), vendeur reçoit taux fixe
            // GrossAmount = ProductTotal + DeliveryFee
            // BasePlatformCommission = GrossAmount × taux (commission plateforme)
            // NetSupplierAmount = GrossAmount - BasePlatformCommission (vendeur jamais pénalisé par ambassadeur/promo)
            var grossAmount = result.ProductTotal + result.DeliveryFee;
            var basePlatformCommission = grossAmount * SellerCommissionRate;

            result.PlatformCommission = basePlatformCommission + result.AmbassadorCommission + result.PromoDiscountAmount;
            result.SellerAmount = grossAmount - basePlatformCommission;

            result.PlatformRevenue = result.ActualPaidAmount;
            result.PlatformCosts = result.SellerAmount + result.AmbassadorCommission + result.PromoDiscountAmount;
            result.PlatformProfit = basePlatformCommission - result.AmbassadorCommission - result.PromoDiscountAmount;

            // ÉTAPE 7: Snapshot JSON pour audit complet
            result.CalculationSnapshot = JsonSerializer.Serialize(new
            {
                OrderId = order.Id,
                CompanyId = companyId,
                CalculatedAt = DateTimeOffset.UtcNow,
                Model = "Amazon-Like-Fixed",
                Formula = new {
                    GrossAmount = $"{grossAmount:C} = ProductTotal + DeliveryFee",
                    BasePlatformCommission = $"{basePlatformCommission:C} = GrossAmount × {SellerCommissionRate:P}",
                    TotalPlatformCommission = $"{result.PlatformCommission:C} = Base + Ambassador + Promo",
                    NetSupplierAmount = $"{result.SellerAmount:C} = GrossAmount - BaseCommission"
                },
                Breakdown = new {
                    ProductTotal = result.ProductTotal,
                    DeliveryFee = result.DeliveryFee,
                    GrossAmount = grossAmount,
                    SellerCommissionRate = result.SellerCommissionRate,
                    BasePlatformCommission = basePlatformCommission,
                    AmbassadorCost = result.AmbassadorCommission,
                    PromoCost = result.PromoDiscountAmount,
                    TotalPlatformCommission = result.PlatformCommission,
                    NetSupplierAmount = result.SellerAmount,
                    ActualPaidByClient = result.ActualPaidAmount,
                    PlatformProfit = result.PlatformProfit
                },
                PromoCode = result.HasPromoCode ? new { Applied = true, Amount = result.PromoDiscountAmount } : null,
                Ambassador = result.HasAmbassador ? new { Applied = true, Amount = result.AmbassadorCommission, Rate = result.AmbassadorRate, Id = result.AmbassadorId } : null
            }, new JsonSerializerOptions { WriteIndented = true });

            return result;
        }

        /// <summary>
        /// Récupère le taux de commission vendeur depuis la configuration
        /// </summary>
        private async Task<decimal> GetSupplierCommissionRateAsync()
        {
            var config = await _context.SystemConfiguration
                .FirstOrDefaultAsync(c => c.ConfigKey == "SUPPLIER_COMMISSION_RATE" && c.IsActive);

            if (config != null && decimal.TryParse(config.ConfigValue, out var rate))
            {
                return rate;
            }

            // Valeur par défaut : 10%
            return 0.10m;
        }

        /// <summary>
        /// Calcule la commission ambassadeur pour cette commande
        /// </summary>
        private async Task<AmbassadorCommission?> CalculateAmbassadorCommissionAsync(Order order, int companyId)
        {
            // Vérifier si une commission existe déjà
            var existingCommission = order.AmbassadorCommissions
                .FirstOrDefault(ac => ac.CompanyId == companyId);

            if (existingCommission != null)
            {
                return existingCommission;
            }

            // Si pas de commission existante, calculer (basé sur AmbassadorPlan)
            var ambassador = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == order.ReferredByAmbassador);

            if (ambassador == null)
            {
                return null;
            }

            // Récupérer l'abonnement ambassadeur actif
            var subscription = await _context.AmbassadorSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.UserId == ambassador.Id && s.Status == SubscriptionStatus.Active)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (subscription?.Plan == null)
            {
                return null;
            }

            var orderProducts = await _context.OrderProducts
                .AsNoTracking()
                .Include(op => op.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                .Where(op => op.OrderId == order.Id
                    && op.CompanyId == companyId
                    && !op.IsDeleted
                    && op.Status != OrderProductStatus.Cancelled
                    && op.Status != OrderProductStatus.Returned)
                .ToListAsync();

            var productTotal = orderProducts.Sum(op => op.SubTotal);
            var ambassadorRate = subscription.Plan.AmbassadorCommissionPercentage;
            var commissionAmount = productTotal * ambassadorRate;

            return new AmbassadorCommission
            {
                AmbassadorId = ambassador.Id,
                OrderId = order.Id,
                CompanyId = companyId,
                Amount = commissionAmount,
                CommissionRate = ambassadorRate,
                Type = AmbassadorCommissionType.DirectSale,
                Status = AmbassadorCommissionStatus.Pending,
                CurrencyId = order.CurrencyBase
            };
        }
    }

    /// <summary>
    /// Résultat de la répartition financière
    /// </summary>
    public class DistributionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // Montants de base
        public decimal ProductTotal { get; set; }
        public decimal DeliveryFee { get; set; }

        // Vendeur
        public decimal SellerAmount { get; set; }
        public decimal SellerCommissionRate { get; set; }

        // PromoCode
        public bool HasPromoCode { get; set; }
        public decimal PromoDiscountAmount { get; set; }
        public decimal ActualPaidAmount { get; set; }

        // Ambassadeur
        public bool HasAmbassador { get; set; }
        public string? AmbassadorId { get; set; }
        public decimal AmbassadorCommission { get; set; }
        public decimal AmbassadorRate { get; set; }

        // Plateforme
        public decimal PlatformCommission { get; set; }
        public decimal PlatformRevenue { get; set; }
        public decimal PlatformCosts { get; set; }
        public decimal PlatformProfit { get; set; }

        // Audit
        public string CalculationSnapshot { get; set; } = string.Empty;

        public static DistributionResult Failed(string message)
        {
            return new DistributionResult
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}
