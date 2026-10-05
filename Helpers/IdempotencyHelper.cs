using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Helpers
{
    /// <summary>
    /// Helper pour garantir l'idempotence des opérations critiques
    /// </summary>
    public static class IdempotencyHelper
    {
        /// <summary>
        /// Vérifie si un BankTransferPayment peut être traité (n'est pas déjà approuvé/rejeté)
        /// </summary>
        public static bool CanProcessBankTransfer(BankTransferPayment transfer)
        {
            if (transfer == null)
                throw new ArgumentNullException(nameof(transfer));

            return transfer.Status == BankTransferStatus.Pending;
        }

        /// <summary>
        /// Vérifie si une transaction peut être modifiée
        /// </summary>
        public static bool CanModifyTransaction(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            // Une transaction terminée ou échouée ne peut plus être modifiée
            return transaction.Status == TransactionStatus.Pending;
        }

        /// <summary>
        /// Vérifie si une commande existe déjà pour éviter les doublons
        /// </summary>
        public static async Task<bool> OrderExistsAsync(AppDbContext context, string userId, int orderId)
        {
            return await context.Orders
                .AnyAsync(o => o.UserId == userId && o.Id == orderId);
        }

        /// <summary>
        /// Vérifie si une commission a déjà été créée pour une commande
        /// </summary>
        public static async Task<bool> CommissionExistsForOrderAsync(AppDbContext context, int orderId)
        {
            return await context.AmbassadorCommissions
                .AnyAsync(c => c.OrderId == orderId && !c.IsDeleted);
        }

        /// <summary>
        /// Vérifie si une commission peut être payée
        /// </summary>
        public static bool CanPayCommission(AmbassadorCommission commission)
        {
            if (commission == null)
                throw new ArgumentNullException(nameof(commission));

            // Une commission ne peut etre payee que si elle est disponible.
            return commission.Status == AmbassadorCommissionStatus.Available;
        }

        /// <summary>
        /// Vérifie si un wallet peut être débité
        /// </summary>
        public static bool CanDebitWallet(Wallet wallet, decimal amount)
        {
            if (wallet == null)
                throw new ArgumentNullException(nameof(wallet));

            return wallet.Balance >= amount;
        }

        /// <summary>
        /// Génère un ID de transaction unique avec vérification
        /// </summary>
        public static async Task<string> GenerateUniqueTransactionIdAsync(AppDbContext context)
        {
            string transactionId;
            int attempts = 0;
            const int maxAttempts = 10;

            do
            {
                transactionId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
                attempts++;

                if (attempts >= maxAttempts)
                    throw new InvalidOperationException("Impossible de générer un ID de transaction unique après plusieurs tentatives");

            } while (await context.Transactions.AnyAsync(t => t.OrderId == transactionId));

            return transactionId;
        }

        /// <summary>
        /// Vérifie si un abonnement peut être créé (pas de doublon actif)
        /// </summary>
        public static async Task<bool> CanCreateSubscriptionAsync(AppDbContext context, string userId, int planId)
        {
            // Vérifier s'il existe déjà un abonnement actif pour ce plan
            var existingActiveSubscription = await context.AmbassadorSubscriptions
                .AnyAsync(s => s.UserId == userId 
                            && s.PlanId == planId 
                            && s.Status == SubscriptionStatus.Active
                            && s.EndDate > DateTimeOffset.UtcNow);

            return !existingActiveSubscription;
        }

        /// <summary>
        /// Vérifie si un retrait est déjà en cours pour l'utilisateur
        /// </summary>
        public static async Task<bool> HasPendingWithdrawalAsync(AppDbContext context, string userId)
        {
            return await context.BankTransferPayments
                .AnyAsync(b => b.UserId == userId 
                            && b.Direction == BankTransferDirection.Withdrawal
                            && b.Status == BankTransferStatus.Pending);
        }

        /// <summary>
        /// Vérifie si un dépôt est déjà en cours de traitement
        /// </summary>
        public static async Task<bool> HasPendingDepositAsync(AppDbContext context, string userId, decimal amount)
        {
            // Vérifier les dépôts des 5 dernières minutes avec le même montant
            var fiveMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-5);
            
            return await context.BankTransferPayments
                .AnyAsync(b => b.UserId == userId 
                            && b.Direction == BankTransferDirection.Deposit
                            && b.Amount == amount
                            && b.Status == BankTransferStatus.Pending
                            && b.SubmittedAt >= fiveMinutesAgo);
        }
    }
}
