using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{
    public interface IWalletService
    {
        Task<Wallet> GetOrCreateWalletAsync(string userId, WalletType type, int currencyBase = 1);
        Task<Wallet?> GetWalletAsync(string userId, WalletType type, int currencyBase = 1);
        Task<decimal> GetBalanceAsync(string userId, WalletType type, int currencyBase = 1);
        Task<WalletTransaction> CreditAsync(string userId, WalletType type, decimal amount, WalletTransactionType transactionType, string? transactionId = null, string? description = null, int currencyBase = 1);
        Task<WalletTransaction> DebitAsync(string userId, WalletType type, decimal amount, WalletTransactionType transactionType, string? transactionId = null, string? description = null, int currencyBase = 1);
    }

    public class WalletService : IWalletService
    {
        private readonly AppDbContext _context;

        public WalletService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Wallet?> GetWalletAsync(string userId, WalletType type, int currencyBase = 1)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w =>
                    w.UserId == userId &&
                    w.Type == type &&
                    w.CurrencyBase == currencyBase &&
                    !w.IsDeleted);
        }

        public async Task<Wallet> GetOrCreateWalletAsync(string userId, WalletType type, int currencyBase = 1)
        {
            var wallet = await GetWalletAsync(userId, type, currencyBase);
            if (wallet != null)
            {
                return wallet;
            }

            var now = DateTimeOffset.UtcNow;
            wallet = new Wallet
            {
                UserId = userId,
                Type = type,
                CurrencyBase = currencyBase,
                Balance = 0m,
                CreatedAt = now,
                LastUpdated = now,
                UpdatedAt = now
            };

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
            return wallet;
        }

        public async Task<decimal> GetBalanceAsync(string userId, WalletType type, int currencyBase = 1)
        {
            var wallet = await GetWalletAsync(userId, type, currencyBase);
            return wallet?.Balance ?? 0m;
        }

        public async Task<WalletTransaction> CreditAsync(string userId, WalletType type, decimal amount, WalletTransactionType transactionType, string? transactionId = null, string? description = null, int currencyBase = 1)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Le montant du credit doit etre positif.");
            }

            var wallet = await GetOrCreateWalletAsync(userId, type, currencyBase);
            var now = DateTimeOffset.UtcNow;
            wallet.Balance += amount;
            wallet.LastUpdated = now;
            wallet.UpdatedAt = now;

            var walletTransaction = CreateWalletTransaction(wallet, userId, type, amount, transactionType, transactionId, description);
            _context.WalletTransactions.Add(walletTransaction);
            return walletTransaction;
        }

        public async Task<WalletTransaction> DebitAsync(string userId, WalletType type, decimal amount, WalletTransactionType transactionType, string? transactionId = null, string? description = null, int currencyBase = 1)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Le montant du debit doit etre positif.");
            }

            var wallet = await GetOrCreateWalletAsync(userId, type, currencyBase);
            if (wallet.Balance < amount)
            {
                throw new InvalidOperationException($"Solde insuffisant dans le wallet {type}. Disponible: {wallet.Balance:F2}, demande: {amount:F2}.");
            }

            var now = DateTimeOffset.UtcNow;
            wallet.Balance -= amount;
            wallet.LastUpdated = now;
            wallet.UpdatedAt = now;

            var walletTransaction = CreateWalletTransaction(wallet, userId, type, -amount, transactionType, transactionId, description);
            _context.WalletTransactions.Add(walletTransaction);
            return walletTransaction;
        }

        private static WalletTransaction CreateWalletTransaction(Wallet wallet, string userId, WalletType type, decimal signedAmount, WalletTransactionType transactionType, string? transactionId, string? description)
        {
            return new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = signedAmount,
                Balance = wallet.Balance,
                CurrencyBase = wallet.CurrencyBase,
                WalletType = type,
                Type = transactionType,
                TransactionId = transactionId,
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
