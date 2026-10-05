using System;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service d'audit automatique pour créer des PlatformTransaction à chaque flux financier
    /// Assure la traçabilité complète et la réconciliation comptable
    /// </summary>
    public interface IPlatformTransactionAuditService
    {
        // Méthodes existantes (ordre conservé)
        Task RecordOrderRevenueAsync(int orderId, decimal amount, string description = null);
        Task RecordSupplierPaymentAsync(int orderId, int companyId, decimal amount, string description = null);
        Task RecordAmbassadorCommissionAsync(int orderId, string ambassadorId, decimal amount, string description = null, int? commissionId = null);
        Task RecordPromoDiscountAsync(int orderId, decimal amount, string promoCode, string description = null);
        Task RecordDistributionAsync(DistributionResult distribution, int orderId, int companyId, decimal? actualSupplierAmountOverride = null, bool recordPromoForThisDistribution = true);
        
        Task RecordUserTransferAsync(string fromUserId, string toUserId, decimal amount, string externalTransactionId = null, string description = null);
        
        // NOUVELLES MÉTHODES OBLIGATOIRES pour audit complet
        Task RecordUserDepositAsync(string userId, decimal amount, string method, string externalTransactionId = null, string description = null);
        Task RecordUserWithdrawalAsync(string userId, decimal amount, decimal bankFees, string method, string externalTransactionId = null, string description = null);
        Task RecordWithdrawalReversalAsync(string userId, decimal amount, decimal bankFees, string originalTransactionId, string description = null);
        Task RecordBankFeesAsync(decimal amount, string transactionId, string description = null);
        Task RecordSubscriptionPaymentAsync(string userId, decimal amount, int subscriptionId, string description = null);
        Task RecordSubscriptionRefundAsync(string userId, decimal amount, int subscriptionId, string description = null);
        Task RecordBonusPaymentAsync(string userId, decimal amount, string bonusType, string description = null);
        Task RecordPlatformCommissionAsync(int orderId, decimal amount, decimal rate, string description = null);
        Task RecordProcessingFeesAsync(decimal amount, string provider, string externalTransactionId = null, string description = null);
        Task RecordRefundAsync(int? orderId, string userId, decimal amount, string reason, string description = null);
        
        // Méthodes utilitaires pour idempotence
        Task<bool> IsTransactionAlreadyAuditedAsync(string externalTransactionId, PlatformTransactionCategory category);
        Task<PlatformTransaction> GetExistingAuditAsync(string externalTransactionId, PlatformTransactionCategory category);
    }

    public class PlatformTransactionAuditService : IPlatformTransactionAuditService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PlatformTransactionAuditService> _logger;

        public PlatformTransactionAuditService(AppDbContext context, ILogger<PlatformTransactionAuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Enregistre le revenu d'une commande (argent reçu du client)
        /// </summary>
        public async Task RecordOrderRevenueAsync(int orderId, decimal amount, string description = null)
        {
            if (amount <= 0) return;

            var externalId = $"order_payment_{orderId}";
            if (await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.OrderPayment))
            {
                return;
            }

            // Récupérer le wallet de la plateforme
            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null)
            {
                _logger.LogWarning("Platform wallet not found, skipping transaction audit");
                return;
            }

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Revenue,
                Category = PlatformTransactionCategory.OrderPayment,
                Amount = amount,
                Description = description ?? $"Revenu commande #{orderId}",
                ExternalTransactionId = externalId,
                OrderId = orderId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Transaction SQL pour atomicité wallet + audit
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, amount);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"Platform revenue recorded: Order {orderId}, Amount {amount:C}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Enregistre le paiement à un vendeur (argent sortant)
        /// </summary>
        public async Task RecordSupplierPaymentAsync(int orderId, int companyId, decimal amount, string description = null)
        {
            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.SellerPayment,
                Amount = -amount,
                Description = description ?? $"Paiement vendeur - Commande #{orderId}",
                OrderId = orderId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Transaction SQL pour atomicité wallet + audit
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, -amount);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"Seller payment recorded: Order {orderId}, Company {companyId}, Amount {amount:C}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Enregistre la commission ambassadeur (argent sortant)
        /// </summary>
        public async Task RecordAmbassadorCommissionAsync(int orderId, string ambassadorId, decimal amount, string description = null, int? commissionId = null)
        {
            if (amount == 0) return; // Pas de commission = pas d'enregistrement

            var externalId = commissionId.HasValue ? $"ambassador_commission_{commissionId.Value}" : null;
            if (!string.IsNullOrEmpty(externalId)
                && await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.AmbassadorCommission))
            {
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.AmbassadorCommission,
                Amount = -amount,
                Description = description ?? $"Commission ambassadeur - Commande #{orderId}",
                OrderId = orderId,
                SellerId = ambassadorId,
                ExternalTransactionId = externalId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Transaction SQL pour atomicité wallet + audit
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, -amount);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"Ambassador commission recorded: Order {orderId}, Ambassador {ambassadorId}, Amount {amount:C}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Enregistre la remise promo (coût marketing)
        /// </summary>
        public async Task RecordPromoDiscountAsync(int orderId, decimal amount, string promoCode, string description = null)
        {
            if (amount == 0) return; // Pas de promo = pas d'enregistrement

            var externalId = $"promo_{orderId}_{promoCode}";
            if (await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.PromoDiscount))
            {
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.PromoDiscount, // CORRIGÉ : nouvelle catégorie spécifique
                Amount = 0m,
                Description = description ?? $"Remise promo '{promoCode}' - Commande #{orderId} (montant applique: {amount:C})",
                ExternalTransactionId = externalId,
                OrderId = orderId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Transaction SQL pour atomicité wallet + audit
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, 0m);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"Promo discount recorded: Order {orderId}, Code {promoCode}, Amount {amount:C}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Récupère le wallet de la plateforme (utilisateur avec code 00000000)
        /// CORRIGÉ : Crée automatiquement si n'existe pas
        /// </summary>
        private async Task<Wallet> GetPlatformWalletAsync()
        {
            // 1. Chercher l'utilisateur plateforme existant
            var platformUser = await _context.Users
                .FirstOrDefaultAsync(u => u.ReferralCode == "00000000");

            // 2. Créer l'utilisateur plateforme s'il n'existe pas
            if (platformUser == null)
            {
                _logger.LogWarning("Platform user not found, creating default platform user");
                
                platformUser = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "platform@mcbusiness.com",
                    Email = "platform@mcbusiness.com",
                    EmailConfirmed = true,
                    ReferralCode = "00000000",
                    FirstName = "MCBusiness",
                    LastName = "Platform",
                    PhoneNumberConfirmed = true,
                    Balance = 0m,
                    RegistrationDate = DateTimeOffset.UtcNow
                };

                _context.Users.Add(platformUser);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Platform user created with ID: {platformUser.Id}");
            }

            // 3. Chercher le wallet de la plateforme
            var wallet = await _context.Wallets
                .Where(w => w.UserId == platformUser.Id && w.Type == WalletType.Platform && !w.IsDeleted)
                .OrderByDescending(w => w.CurrencyBase == 1)
                .ThenBy(w => w.Id)
                .FirstOrDefaultAsync();

            // 4. Créer le wallet s'il n'existe pas
            if (wallet == null)
            {
                _logger.LogWarning("Platform wallet not found, creating default platform wallet");
                
                var now = DateTimeOffset.UtcNow;
                wallet = new Wallet
                {
                    UserId = platformUser.Id,
                    Type = WalletType.Platform,
                    Balance = 0m,
                    CurrencyBase = 1, // HTG par défaut
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastUpdated = now
                };

                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Platform wallet created with ID: {wallet.Id}");
            }

            return wallet;
        }

        /// <summary>
        /// Enregistre toute la distribution en UNE SEULE transaction atomique.
        /// Idempotent : ne double pas si rappelé pour le même (orderId, companyId).
        /// actualSupplierAmountOverride : montant réellement versé au vendeur (aligné avec SellerPayment.SellerAmount).
        /// recordPromoForThisDistribution : false pour éviter de compter la promo plusieurs fois en commande multi-vendeurs.
        /// </summary>
        public async Task RecordDistributionAsync(DistributionResult distribution, int orderId, int companyId, decimal? actualSupplierAmountOverride = null, bool recordPromoForThisDistribution = true)
        {
            var externalId = $"dist_{orderId}_{companyId}";
            decimal supplierAmountDebit = actualSupplierAmountOverride ?? distribution.SellerAmount;

            var existingAudit = await GetExistingAuditAsync(externalId, PlatformTransactionCategory.SellerPayment);
            if (existingAudit != null)
            {
                if (existingAudit.Amount == 0m && supplierAmountDebit > 0m)
                {
                    await CorrectZeroDistributionAuditAsync(
                        existingAudit,
                        supplierAmountDebit,
                        distribution.SellerCommissionRate);
                }

                _logger.LogInformation("Distribution audit already recorded for Order {OrderId}, Company {CompanyId}, skipping", orderId, companyId);
                return;
            }

            await RecordSellerPaymentDebitSqlAsync(
                orderId,
                companyId,
                supplierAmountDebit,
                distribution.SellerCommissionRate,
                externalId);

            if (recordPromoForThisDistribution && distribution.HasPromoCode && distribution.PromoDiscountAmount > 0)
            {
                await RecordPromoDistributionPlaceholderSqlAsync(orderId, distribution.PromoDiscountAmount);
            }

            _logger.LogInformation(
                "Distribution audit completed for Order {OrderId}: SupplierPaid={SellerAmount:C}, AmbassadorPending={AmbassadorCommission:C}, PromoRecorded={PromoRecorded}",
                orderId,
                supplierAmountDebit,
                distribution.AmbassadorCommission,
                recordPromoForThisDistribution && distribution.HasPromoCode ? distribution.PromoDiscountAmount : 0m);
            return;

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null)
            {
                _logger.LogWarning("Platform wallet not found, skipping distribution audit");
                return;
            }

            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                var transactions = new List<PlatformTransaction>();

                // 2. Paiement vendeur (argent sortant) - montant réel versé au vendeur pour cohérence audit
                var supplierTransaction = new PlatformTransaction
                {
                    Type = PlatformTransactionType.Expense,
                    Category = PlatformTransactionCategory.SellerPayment,
                    Amount = -supplierAmountDebit,
                    Description = $"Paiement vendeur (Commission: {distribution.SellerCommissionRate:P})",
                    OrderId = orderId,
                    WalletId = platformWallet.Id,
                    ExternalTransactionId = externalId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                transactions.Add(supplierTransaction);

                // La commission ambassadeur est creee comme commission en attente a la livraison.
                // Le debit plateforme reel est enregistre au moment du versement de la commission.

                // 4. Remise promo (une seule fois par commande pour éviter N× en multi-vendeurs)
                if (recordPromoForThisDistribution && distribution.HasPromoCode && distribution.PromoDiscountAmount > 0)
                {
                    var promoTransaction = new PlatformTransaction
                    {
                        Type = PlatformTransactionType.Expense,
                        Category = PlatformTransactionCategory.PromoDiscount,
                        Amount = 0m,
                        Description = $"Remise promo appliquee - Commande #{orderId} (montant: {distribution.PromoDiscountAmount:C})",
                        OrderId = orderId,
                        WalletId = platformWallet.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    transactions.Add(promoTransaction);
                }

                _context.PlatformTransactions.AddRange(transactions);

                decimal totalBalanceChange = 0;
                foreach (var txn in transactions)
                    totalBalanceChange += txn.Amount;

                ApplyPlatformBalanceChange(platformWallet, totalBalanceChange);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation(
                    "Distribution audit completed for Order {OrderId}: SupplierPaid={SellerAmount:C}, AmbassadorPending={AmbassadorCommission:C}, PromoRecorded={PromoRecorded}, TotalWalletChange={TotalWalletChange:C}, NewPlatformBalance={Balance:C}",
                    orderId, supplierAmountDebit, distribution.AmbassadorCommission, recordPromoForThisDistribution && distribution.HasPromoCode ? distribution.PromoDiscountAmount : 0m, totalBalanceChange, platformWallet.Balance);
            }
            catch (Exception ex)
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error recording distribution audit for Order {OrderId}, Company {CompanyId}", orderId, companyId);
                throw;
            }
        }

        /// <summary>
        /// Trace un transfert entre utilisateurs (audit uniquement, solde plateforme inchangé).
        /// </summary>
        private async Task RecordSellerPaymentDebitSqlAsync(
            int orderId,
            int companyId,
            decimal supplierAmountDebit,
            decimal sellerCommissionRate,
            string externalId)
        {
            if (supplierAmountDebit <= 0)
            {
                return;
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await BeginTransactionIfNeededAsync();
                try
                {
                    if (await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.SellerPayment))
                    {
                        if (dbTransaction != null) await dbTransaction.CommitAsync();
                        return;
                    }

                    var platformWallet = await GetPlatformWalletAsync();
                    if (platformWallet == null)
                    {
                        _logger.LogWarning("Platform wallet not found, skipping seller payment debit audit");
                        if (dbTransaction != null) await dbTransaction.CommitAsync();
                        return;
                    }

                    var now = DateTimeOffset.UtcNow;
                    var debitAmount = Math.Abs(supplierAmountDebit);
                    var transactionAmount = -debitAmount;
                    var description = $"Paiement vendeur (Commission: {sellerCommissionRate:P})";
                    var transactionType = PlatformTransactionType.Expense.ToString();
                    var category = PlatformTransactionCategory.SellerPayment.ToString();

                    var updatedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""Wallets""
SET ""Balance"" = ""Balance"" - {debitAmount},
    ""UpdatedAt"" = {now},
    ""LastUpdated"" = {now}
WHERE ""Id"" = {platformWallet.Id}
  AND ""IsDeleted"" = FALSE
  AND ""Balance"" >= {debitAmount}");

                    if (updatedRows != 1)
                    {
                        throw new InvalidOperationException(
                            $"Solde plateforme insuffisant pour journaliser le paiement vendeur. WalletId: {platformWallet.Id}, Montant requis: {debitAmount:F2}.");
                    }

                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""PlatformTransactions""
    (""Type"", ""Category"", ""Amount"", ""Description"", ""ExternalTransactionId"", ""CreatedAt"", ""OrderId"", ""WalletId"", ""IsManual"", ""IsDeleted"")
VALUES
    ({transactionType}, {category}, {transactionAmount}, {description}, {externalId}, {now}, {orderId}, {platformWallet.Id}, FALSE, FALSE)");

                    if (dbTransaction != null) await dbTransaction.CommitAsync();

                    _logger.LogInformation(
                        "Seller payment debit audited with SQL for Order {OrderId}, Company {CompanyId}, Amount {Amount:C}",
                        orderId,
                        companyId,
                        transactionAmount);
                }
                catch (Exception ex)
                {
                    if (dbTransaction != null) await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Error recording seller payment debit audit for Order {OrderId}, Company {CompanyId}", orderId, companyId);
                    throw;
                }
            });
        }

        private async Task RecordPromoDistributionPlaceholderSqlAsync(int orderId, decimal promoDiscountAmount)
        {
            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null)
            {
                _logger.LogWarning("Platform wallet not found, skipping promo distribution placeholder");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var transactionType = PlatformTransactionType.Expense.ToString();
            var category = PlatformTransactionCategory.PromoDiscount.ToString();
            var description = $"Remise promo appliquee - Commande #{orderId} (montant: {promoDiscountAmount:C})";

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""PlatformTransactions""
    (""Type"", ""Category"", ""Amount"", ""Description"", ""CreatedAt"", ""OrderId"", ""WalletId"", ""IsManual"", ""IsDeleted"")
VALUES
    ({transactionType}, {category}, {0m}, {description}, {now}, {orderId}, {platformWallet.Id}, FALSE, FALSE)");
        }

        public async Task RecordUserTransferAsync(string fromUserId, string toUserId, decimal amount, string externalTransactionId = null, string description = null)
        {
            if (amount <= 0) return;
            if (!string.IsNullOrEmpty(externalTransactionId) &&
                await _context.PlatformTransactions.AnyAsync(pt => pt.ExternalTransactionId == externalTransactionId && pt.Category == PlatformTransactionCategory.ManualAdjustment && !pt.IsDeleted))
            {
                _logger.LogInformation("Transfer audit {ExternalId} already recorded, skipping", externalTransactionId);
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Revenue,
                Category = PlatformTransactionCategory.ManualAdjustment,
                Amount = 0,
                Description = description ?? $"Transfert utilisateur {fromUserId} â†’ {toUserId}: {amount:C}",
                SellerId = fromUserId,
                ExternalTransactionId = externalTransactionId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                IsManual = false
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, 0,
                $"User transfer recorded: {fromUserId} â†’ {toUserId}, Amount {amount:C}");
        }

        // ============== NOUVELLES MÉTHODES OBLIGATOIRES ==============

        /// <summary>
        /// Enregistre un dépôt utilisateur (argent entrant)
        /// </summary>
        private async Task CorrectZeroDistributionAuditAsync(
            PlatformTransaction existingAudit,
            decimal supplierAmountDebit,
            decimal sellerCommissionRate)
        {
            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null)
            {
                _logger.LogWarning("Platform wallet not found, skipping zero distribution audit correction");
                return;
            }

            var correctedAmount = -Math.Abs(supplierAmountDebit);
            var balanceDelta = correctedAmount - existingAudit.Amount;

            if (balanceDelta == 0m)
            {
                return;
            }

            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                existingAudit.Amount = correctedAmount;
                existingAudit.Description = $"Paiement vendeur (Commission: {sellerCommissionRate:P})";
                existingAudit.WalletId = platformWallet.Id;

                ApplyPlatformBalanceChange(platformWallet, balanceDelta);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation(
                    "Zero distribution audit corrected for Order {OrderId}, ExternalId {ExternalId}: Amount={Amount:C}, BalanceDelta={BalanceDelta:C}",
                    existingAudit.OrderId,
                    existingAudit.ExternalTransactionId,
                    correctedAmount,
                    balanceDelta);
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task RecordUserDepositAsync(string userId, decimal amount, string method, string externalTransactionId = null, string description = null)
        {
            if (amount <= 0) return;

            // Vérifier idempotence
            if (!string.IsNullOrEmpty(externalTransactionId) && 
                await IsTransactionAlreadyAuditedAsync(externalTransactionId, PlatformTransactionCategory.UserDeposit))
            {
                _logger.LogInformation($"Deposit transaction {externalTransactionId} already audited, skipping");
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Revenue,
                Category = PlatformTransactionCategory.UserDeposit,
                Amount = amount,
                Description = description ?? $"Dépôt utilisateur via {method}",
                SellerId = userId,
                ExternalTransactionId = externalTransactionId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Transaction SQL pour atomicité wallet + audit
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, amount);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"User deposit recorded: User {userId}, Amount {amount:C}, Method {method}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Enregistre un retrait utilisateur avec frais bancaires (argent sortant)
        /// </summary>
        public async Task RecordUserWithdrawalAsync(string userId, decimal amount, decimal bankFees, string method, string externalTransactionId = null, string description = null)
        {
            if (amount <= 0) return;

            // Vérifier idempotence
            if (!string.IsNullOrEmpty(externalTransactionId) && 
                await IsTransactionAlreadyAuditedAsync(externalTransactionId, PlatformTransactionCategory.UserWithdrawal))
            {
                _logger.LogInformation($"Withdrawal transaction {externalTransactionId} already audited, skipping");
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            // Transaction SQL pour atomicité
            await using var transaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Retrait utilisateur
                var withdrawalTransaction = new PlatformTransaction
                {
                    Type = PlatformTransactionType.Expense,
                    Category = PlatformTransactionCategory.UserWithdrawal,
                    Amount = -amount,
                    Description = description ?? $"Retrait utilisateur via {method}",
                    SellerId = userId,
                    ExternalTransactionId = externalTransactionId,
                    WalletId = platformWallet.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _context.PlatformTransactions.Add(withdrawalTransaction);

                // 2. Frais bancaires (si applicable)
                if (bankFees > 0)
                {
                    var feesTransaction = new PlatformTransaction
                    {
                        Type = PlatformTransactionType.Expense,
                        Category = PlatformTransactionCategory.BankFees,
                        Amount = -bankFees,
                        Description = $"Frais bancaires - Retrait {method}",
                        SellerId = userId,
                        ExternalTransactionId = $"{externalTransactionId}_fees",
                        WalletId = platformWallet.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    _context.PlatformTransactions.Add(feesTransaction);
                }

                // 3. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, -(amount + bankFees));
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                _logger.LogInformation($"User withdrawal recorded: User {userId}, Amount {amount:C}, Fees {bankFees:C}, Method {method}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Enregistre des frais bancaires seuls
        /// </summary>
        public async Task RecordBankFeesAsync(decimal amount, string transactionId, string description = null)
        {
            if (amount <= 0) return;

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.BankFees,
                Amount = -amount,
                Description = description ?? "Frais bancaires",
                ExternalTransactionId = transactionId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Transaction SQL pour atomicité wallet + audit
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, -amount);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"Bank fees recorded: Amount {amount:C}, Transaction {transactionId}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Enregistre un paiement d'abonnement (argent entrant)
        /// </summary>
        public async Task RecordSubscriptionPaymentAsync(string userId, decimal amount, int subscriptionId, string description = null)
        {
            if (amount <= 0) return;

            var externalId = $"subscription_payment_{subscriptionId}";
            if (await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.SubscriptionPayment))
            {
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Revenue,
                Category = PlatformTransactionCategory.SubscriptionPayment,
                Amount = amount,
                Description = description ?? $"Paiement abonnement #{subscriptionId}",
                ExternalTransactionId = externalId,
                AmbassadorSubscriptionId = subscriptionId,
                SellerId = userId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, amount, 
                $"Subscription payment recorded: User {userId}, Subscription {subscriptionId}, Amount {amount:C}");
        }

        /// <summary>
        /// Enregistre un remboursement d'abonnement (argent sortant)
        /// </summary>
        public async Task RecordSubscriptionRefundAsync(string userId, decimal amount, int subscriptionId, string description = null)
        {
            if (amount <= 0) return;

            var externalId = $"subscription_refund_{subscriptionId}";
            if (await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.SubscriptionRefund))
            {
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.SubscriptionRefund,
                Amount = -amount,
                Description = description ?? $"Remboursement abonnement #{subscriptionId}",
                ExternalTransactionId = externalId,
                AmbassadorSubscriptionId = subscriptionId,
                SellerId = userId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, -amount, 
                $"Subscription refund recorded: User {userId}, Subscription {subscriptionId}, Amount {amount:C}");
        }

        /// <summary>
        /// Enregistre un paiement de bonus utilisateur (argent sortant)
        /// </summary>
        public async Task RecordBonusPaymentAsync(string userId, decimal amount, string bonusType, string description = null)
        {
            if (amount <= 0) return;

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.BonusPayment,
                Amount = -amount,
                Description = description ?? $"Bonus {bonusType}",
                SellerId = userId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, -amount, 
                $"Bonus payment recorded: User {userId}, Type {bonusType}, Amount {amount:C}");
        }

        /// <summary>
        /// Enregistre la commission plateforme (argent entrant)
        /// </summary>
        public async Task RecordPlatformCommissionAsync(int orderId, decimal amount, decimal rate, string description = null)
        {
            if (amount <= 0) return;

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Revenue,
                Category = PlatformTransactionCategory.Commission,
                Amount = amount,
                Description = description ?? $"Commission plateforme ({rate:P}) - Commande #{orderId}",
                OrderId = orderId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, amount, 
                $"Platform commission recorded: Order {orderId}, Amount {amount:C}, Rate {rate:P}");
        }

        /// <summary>
        /// Enregistre des frais de traitement (MonCash, Stripe, etc.)
        /// </summary>
        public async Task RecordProcessingFeesAsync(decimal amount, string provider, string externalTransactionId = null, string description = null)
        {
            if (amount <= 0) return;

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.ProcessingFees,
                Amount = -amount,
                Description = description ?? $"Frais de traitement {provider}",
                ExternalTransactionId = externalTransactionId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, -amount, 
                $"Processing fees recorded: Provider {provider}, Amount {amount:C}");
        }

        /// <summary>
        /// Enregistre un remboursement général (argent sortant)
        /// </summary>
        public async Task RecordRefundAsync(int? orderId, string userId, decimal amount, string reason, string description = null)
        {
            if (amount <= 0) return;

            var platformWallet = await GetPlatformWalletAsync();
            if (platformWallet == null) return;

            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.Refund,
                Amount = -amount,
                Description = description ?? $"Remboursement: {reason}",
                OrderId = orderId,
                SellerId = userId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await RecordTransactionAndUpdateWalletAsync(transaction, platformWallet, -amount, 
                $"Refund recorded: User {userId}, Order {orderId}, Amount {amount:C}, Reason {reason}");
        }

        // ============== MÉTHODE UTILITAIRE POUR AUDIT + WALLET ==============

        /// <summary>
        /// Enregistre une PlatformTransaction ET met à jour le solde wallet plateforme
        /// </summary>
        private async Task RecordTransactionAndUpdateWalletAsync(PlatformTransaction transaction, Wallet platformWallet, decimal balanceChange, string logMessage)
        {
            await using var dbTransaction = await BeginTransactionIfNeededAsync();
            try
            {
                // 1. Créer l'audit
                _context.PlatformTransactions.Add(transaction);

                // 2. CRITIQUE : Mettre à jour le solde wallet plateforme
                ApplyPlatformBalanceChange(platformWallet, balanceChange);
                var now = DateTimeOffset.UtcNow;
                platformWallet.UpdatedAt = now;
                platformWallet.LastUpdated = now;
                _context.Wallets.Update(platformWallet);

                await _context.SaveChangesAsync();
                if (dbTransaction != null) await dbTransaction.CommitAsync();

                _logger.LogInformation($"{logMessage}, New platform balance: {platformWallet.Balance:C}");
            }
            catch
            {
                if (dbTransaction != null) await dbTransaction.RollbackAsync();
                throw;
            }
        }

        // ============== MÉTHODES UTILITAIRES POUR IDEMPOTENCE ==============

        /// <summary>
        /// Vérifie si une transaction externe a déjà été auditée
        /// </summary>
        public async Task<bool> IsTransactionAlreadyAuditedAsync(string externalTransactionId, PlatformTransactionCategory category)
        {
            if (string.IsNullOrEmpty(externalTransactionId)) return false;

            return await _context.PlatformTransactions
                .AnyAsync(pt => pt.ExternalTransactionId == externalTransactionId && 
                               pt.Category == category && 
                               !pt.IsDeleted);
        }

        /// <summary>
        /// Récupère une transaction d'audit existante
        /// </summary>
        public async Task<PlatformTransaction> GetExistingAuditAsync(string externalTransactionId, PlatformTransactionCategory category)
        {
            if (string.IsNullOrEmpty(externalTransactionId)) return null;

            return await _context.PlatformTransactions
                .FirstOrDefaultAsync(pt => pt.ExternalTransactionId == externalTransactionId && 
                                          pt.Category == category && 
                                          !pt.IsDeleted);
        }

        public async Task RecordWithdrawalReversalAsync(
            string userId,
            decimal amount,
            decimal bankFees,
            string originalTransactionId,
            string description = null)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(originalTransactionId)) return;

            var externalId = $"withdrawal_reversal_{originalTransactionId}";
            if (await IsTransactionAlreadyAuditedAsync(externalId, PlatformTransactionCategory.ManualAdjustment))
            {
                return;
            }

            var platformWallet = await GetPlatformWalletAsync();
            var totalCredit = amount + Math.Max(0, bankFees);
            var transaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Revenue,
                Category = PlatformTransactionCategory.ManualAdjustment,
                Amount = totalCredit,
                Description = description ?? $"Annulation retrait {originalTransactionId}",
                SellerId = userId,
                ExternalTransactionId = externalId,
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                IsManual = false
            };

            await RecordTransactionAndUpdateWalletAsync(
                transaction,
                platformWallet,
                totalCredit,
                $"Withdrawal reversal recorded: {originalTransactionId}");
        }

        private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionIfNeededAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        }

        private static void ApplyPlatformBalanceChange(Wallet wallet, decimal balanceChange)
        {
            var newBalance = wallet.Balance + balanceChange;
            if (balanceChange < 0 && newBalance < 0)
            {
                throw new InvalidOperationException(
                    $"Solde plateforme insuffisant. WalletId: {wallet.Id}, UserId: {wallet.UserId}, Devise: {wallet.CurrencyBase}, Disponible: {wallet.Balance:F2}, variation demandee: {balanceChange:F2}.");
            }

            wallet.Balance = newBalance;
            var now = DateTimeOffset.UtcNow;
            wallet.UpdatedAt = now;
            wallet.LastUpdated = now;
        }
    }
}
