using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{

    public interface ISellerPaymentService
    {
        Task CreateSellerPaymentIfEligibleAsync(int orderId, int companyId);
        Task<int> ReconcileDeliveredSellerPaymentsAsync(int maxOrders = 50);
    }

    public class SellerPaymentService : ISellerPaymentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SellerPaymentService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IAmazonLikeDistributionService _distributionService;
        private readonly IPlatformTransactionAuditService _auditService;
        private readonly IWalletService _walletService;

        public SellerPaymentService(
            AppDbContext context, 
            ILogger<SellerPaymentService> logger, 
            INotificationService notificationService,
            IAmazonLikeDistributionService distributionService,
            IPlatformTransactionAuditService auditService,
            IWalletService walletService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
            _distributionService = distributionService;
            _auditService = auditService;
            _walletService = walletService;
        }

        public async Task CreateSellerPaymentIfEligibleAsync(int orderId, int companyId)
        {
            // Vérifie si un paiement existe déjà
            var existingPayment = await _context.SellerPayments
                .FirstOrDefaultAsync(sp => sp.OrderId == orderId && sp.CompanyId == companyId && !sp.IsDeleted);

            if (existingPayment != null)
            {
                await EnsureDistributionAuditAsync(existingPayment);
                return;
            }

            var order = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);

            if (order == null)
            {
                _logger.LogWarning("SellerPayment skipped: Order {OrderId} not found", orderId);
                return;
            }

            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refunded)
            {
                _logger.LogInformation("SellerPayment skipped: Order {OrderId} has status {Status}", orderId, order.Status);
                return;
            }

            var completedPayment = await _context.Payments
                .AsNoTracking()
                .Where(p => p.OrderId == orderId
                    && p.Status == PaymentStatus.Completed
                    && !p.IsDeleted)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();

            if (completedPayment == null)
            {
                _logger.LogWarning("SellerPayment skipped: Order {OrderId} has no completed payment", orderId);
                return;
            }

            // Récupère tous les produits de la commande pour cette compagnie
            var orderProducts = await _context.OrderProducts
                .Include(op=> op.Company)
                .Include(op => op.ProductVariant)
                .ThenInclude(pv => pv.Product)
                .Include(op => op.Shipment)
                .Where(op => op.OrderId == orderId
                    && op.CompanyId == companyId
                    && !op.IsDeleted
                    && op.Status != OrderProductStatus.Cancelled
                    && op.Status != OrderProductStatus.Returned)
                .ToListAsync();

            if (orderProducts.Count == 0) return;

            // Vérifie si tous sont livrés
            bool allDelivered = orderProducts.All(op =>
                op.Status == OrderProductStatus.Delivered
                && (op.Shipment == null || op.Shipment.Status == ShipmentStatus.Delivered));

            if (!allDelivered) return;

            // ============== NOUVEAU CALCUL AMAZON-LIKE ==============
            // Utiliser le service de distribution pour calcul équitable
            var distribution = await _distributionService.CalculateDistributionAsync(orderId, companyId);
            
            if (!distribution.Success)
            {
                _logger.LogError($"Failed to calculate distribution for Order {orderId}, Company {companyId}: {distribution.ErrorMessage}");
                return;
            }

            // FORMULE FINANCIERE OFFICIELLE - CALCULS EXPLICITES
            
            // Etape 1 : Calculs de base
            decimal productTotal = orderProducts.Sum(op => op.SubTotal);
            decimal deliveryFee = orderProducts.Sum(op => op.DeliveryFee);
            decimal grossAmount = productTotal + deliveryFee;
            
            // Etape 2 : Commission plateforme (BASE SEULEMENT)
            decimal basePlatformCommission = grossAmount * distribution.SellerCommissionRate;
            
            // Etape 3 : Couts totaux plateforme (base + marketing)
            decimal platformTotalCost = basePlatformCommission 
                + distribution.AmbassadorCommission 
                + distribution.PromoDiscountAmount;
            
            // Etape 4 : REGLE STRICTE - Vendeur ne paie QUE la commission base
            decimal netSupplierAmount = grossAmount - basePlatformCommission;

            // VALIDATIONS OBLIGATOIRES
            if (Math.Abs(grossAmount - (productTotal + deliveryFee)) > 0.01m)
                throw new InvalidOperationException($"Erreur calcul: GrossAmount={grossAmount} != ProductTotal+DeliveryFee={productTotal + deliveryFee}");
            
            if (Math.Abs(netSupplierAmount - (grossAmount - basePlatformCommission)) > 0.01m)
                throw new InvalidOperationException($"Erreur calcul: NetSupplier={netSupplierAmount} != GrossAmount-Commission={grossAmount - basePlatformCommission}");

            var payment = new SellerPayment
            {
                CompanyId = companyId,
                OrderId = orderId,
                CurrencyBase = order.CurrencyBase,
                
                // Champs legacy (compatibilite)
                ProductTotal = productTotal,
                DeliveryFee = deliveryFee,
                PlatformCommission = basePlatformCommission, // Commission base SEULEMENT
                SellerAmount = netSupplierAmount, // CALCULE selon formule officielle
                
                // NOUVEAUX CHAMPS - Formule financiere officielle
                GrossAmount = grossAmount,
                PlatformCommissionTotal = platformTotalCost,
                
                // NOUVEAUX CHAMPS AMAZON-LIKE
                ActualPaidAmount = distribution.ActualPaidAmount,
                PromoDiscountAmount = distribution.PromoDiscountAmount,
                AmbassadorCommissionAmount = distribution.AmbassadorCommission,
                AmbassadorId = distribution.AmbassadorId,
                PlatformCommissionRate = distribution.SellerCommissionRate,
                CalculationSnapshot = distribution.CalculationSnapshot,
                
                OrderProducts = orderProducts,
                PaymentDate = DateTimeOffset.UtcNow,
                Status = PaymentStatus.Pending,
                IsPaidToSeller = false,
                // DateAdded et DateUpdated gérés par BaseEntity
            };

            // LOG CLAIR POUR AUDIT FINANCIER
            _logger.LogInformation(
                $"SellerPayment created with OFFICIAL FORMULA: " +
                $"Order {orderId}, Company {companyId} | " +
                $"GrossAmount={grossAmount:C} (ProductTotal={productTotal:C} + DeliveryFee={deliveryFee:C}) | " +
                $"BasePlatformCommission={basePlatformCommission:C} ({distribution.SellerCommissionRate:P}) | " +
                $"NetSupplierAmount={netSupplierAmount:C} (Gross - Commission) | " +
                $"PlatformTotalCost={platformTotalCost:C} (Base + Ambassador={distribution.AmbassadorCommission:C} + Promo={distribution.PromoDiscountAmount:C}) | " +
                $"PlatformProfit={basePlatformCommission - distribution.AmbassadorCommission - distribution.PromoDiscountAmount:C}");

            _context.SellerPayments.Add(payment);
            await _context.SaveChangesAsync();

            var sellerId = orderProducts[0].Company?.SellerId;
            if (!string.IsNullOrWhiteSpace(sellerId) && netSupplierAmount > 0m)
            {
                var walletTransactionId = $"seller_payment_{payment.Id}";
                var alreadyCredited = await _context.WalletTransactions.AnyAsync(wt =>
                    wt.TransactionId == walletTransactionId &&
                    wt.WalletType == WalletType.Earnings &&
                    wt.Type == WalletTransactionType.Sales &&
                    !wt.IsDeleted);

                if (!alreadyCredited)
                {
                    await _walletService.CreditAsync(
                        sellerId,
                        WalletType.Earnings,
                        netSupplierAmount,
                        WalletTransactionType.Sales,
                        walletTransactionId,
                        $"Gain vendeur commande #{orderId}, paiement #{payment.Id}",
                        payment.CurrencyBase);
                    await _context.SaveChangesAsync();
                }
            }

            await EnsureDistributionAuditAsync(payment, distribution, netSupplierAmount);

            // Notification via le service centralisé
            var UserPlatform = await _context.Users.FirstOrDefaultAsync(u=> u.ReferralCode == "00000000");
            if (UserPlatform != null)
            {
                var notification = new Notification
                {
                    UserId = UserPlatform.Id,
                    Title = "Paiement fournisseur en attente",
                    Message = $"Vous avez un nouveau paiement de la commande #{orderId} en attente de votre approbation pour l'entreprise {orderProducts[0].Company.Name}",
                };
                await _notificationService.NotifyAsync(notification);
            }

            _logger.LogInformation("? SellerPayment créé pour Order {OrderId}, Company {CompanyId}", orderId, companyId);
        }

        public async Task<int> ReconcileDeliveredSellerPaymentsAsync(int maxOrders = 50)
        {
            var candidates = await _context.OrderProducts
                .AsNoTracking()
                .Where(op => !op.IsDeleted
                    && op.Status == OrderProductStatus.Delivered
                    && op.Order.Status == OrderStatus.Delivered
                    && !op.Order.IsDeleted
                    && _context.Payments.Any(p => p.OrderId == op.OrderId
                        && p.Status == PaymentStatus.Completed
                        && !p.IsDeleted))
                .Select(op => new { op.OrderId, op.CompanyId })
                .Distinct()
                .OrderByDescending(x => x.OrderId)
                .Take(maxOrders)
                .ToListAsync();

            var reconciled = 0;

            foreach (var candidate in candidates)
            {
                var externalId = $"dist_{candidate.OrderId}_{candidate.CompanyId}";
                var sellerPayment = await _context.SellerPayments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sp => sp.OrderId == candidate.OrderId
                        && sp.CompanyId == candidate.CompanyId
                        && !sp.IsDeleted);

                var platformTransaction = await _context.PlatformTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pt => pt.ExternalTransactionId == externalId
                        && pt.Category == PlatformTransactionCategory.SellerPayment
                        && !pt.IsDeleted);

                var needsReconciliation = sellerPayment == null
                    || platformTransaction == null
                    || (platformTransaction.Amount == 0m && (sellerPayment.SellerAmount ?? 0m) > 0m);

                if (!needsReconciliation)
                {
                    continue;
                }

                await CreateSellerPaymentIfEligibleAsync(candidate.OrderId, candidate.CompanyId);
                reconciled++;
            }

            return reconciled;
        }

        private async Task EnsureDistributionAuditAsync(
            SellerPayment sellerPayment,
            DistributionResult? distribution = null,
            decimal? supplierAmountOverride = null)
        {
            var orderId = sellerPayment.OrderId;
            var companyId = sellerPayment.CompanyId;

            distribution ??= await _distributionService.CalculateDistributionAsync(orderId, companyId);
            if (!distribution.Success)
            {
                throw new InvalidOperationException(
                    $"Distribution impossible pour la commande {orderId}, vendeur {companyId}: {distribution.ErrorMessage}");
            }

            var supplierAmount = supplierAmountOverride
                ?? sellerPayment.SellerAmount
                ?? distribution.SellerAmount;

            if (supplierAmount <= 0)
            {
                supplierAmount = distribution.SellerAmount;
            }

            var promoAlreadyRecorded = await _context.PlatformTransactions.AnyAsync(pt =>
                pt.OrderId == orderId &&
                pt.Category == PlatformTransactionCategory.PromoDiscount &&
                !pt.IsDeleted);

            await _auditService.RecordDistributionAsync(
                distribution,
                orderId,
                companyId,
                supplierAmount,
                !promoAlreadyRecorded);
        }
    }

}
