using Microsoft.EntityFrameworkCore;
using System.Data;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public interface ISecureFinancialService
    {
        Task<FinancialOperationResult> ProcessWithdrawalAsync(string userId, decimal amount, TransactionMethod method, string description = null, WalletType walletType = WalletType.Customer);
        Task<FinancialOperationResult> ProcessTransferAsync(string fromUserId, string toUserId, decimal amount, string description = null);
        Task<FinancialOperationResult> ProcessDepositAsync(string userId, decimal amount, TransactionMethod method, string description = null);
        Task<FinancialOperationResult> ReverseWithdrawalAsync(string userId, string transactionId, string description = null);
        Task<FinancialOperationResult> CompletePendingDepositAsync(string orderId, string gatewayTransactionId, decimal confirmedAmount);
        Task<decimal> GetAvailableBalanceAsync(string userId);
        Task<bool> ReserveBalanceAsync(string userId, decimal amount, TimeSpan? reservationDuration = null);
        Task<bool> ConfirmBalanceReservationAsync(string userId, decimal amount);
        Task CleanupExpiredBalanceReservationsAsync();
    }

    public class SecureFinancialService : ISecureFinancialService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SecureFinancialService> _logger;
        private readonly IPlatformTransactionAuditService _auditService;
        private readonly TimeSpan _defaultReservationDuration = TimeSpan.FromMinutes(10);

        public SecureFinancialService(
            AppDbContext context, 
            ILogger<SecureFinancialService> logger,
            IPlatformTransactionAuditService auditService)
        {
            _context = context;
            _logger = logger;
            _auditService = auditService;
        }

        public async Task<FinancialOperationResult> ProcessWithdrawalAsync(string userId, decimal amount, TransactionMethod method, string description = null, WalletType walletType = WalletType.Customer)
        {
            if (amount <= 0)
                return new FinancialOperationResult { Success = false, ErrorMessage = "Le montant doit être supérieur à zéro" };

            // IMPORTANT: Utiliser ExecutionStrategy avec Npgsql pour permettre les retries
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                // Utilisation d'une transaction avec isolation level pour éviter les race conditions
                using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Nettoyer les réservations expirées
                await CleanupExpiredBalanceReservationsAsync();

                // Obtenir le wallet avec lock pessimiste
                var wallet = await _context.Wallets
                    .Where(w => w.UserId == userId && w.Type == walletType)
                    .FirstOrDefaultAsync();

                if (wallet == null)
                {
                    return new FinancialOperationResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Portefeuille non trouvé" 
                    };
                }

                // Calculer balance réservée
                var reservedBalance = await GetReservedBalanceAsync(userId, walletType);
                var availableBalance = wallet.Balance - reservedBalance;

                if (availableBalance < amount)
                {
                    return new FinancialOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Solde insuffisant. Disponible: {availableBalance:C}, Demandé: {amount:C}"
                    };
                }

                // Générer ID de transaction unique
                var orderId = await GenerateUniqueOrderIdAsync();

                // Créer la transaction d'abord
                var transactionRecord = new Transaction
                {
                    UserId = userId,
                    WalletId = wallet.Id,
                    WalletType = walletType,
                    Amount = amount,
                    Type = TransactionType.Withdrawal,
                    Method = method,
                    TransactionDate = DateTimeOffset.UtcNow,
                    Status = TransactionStatus.Processing, // Statut intermédiaire
                    OrderId = orderId?.ToString(),
                    Description = description ?? $"Retrait {method}",
                    CurrencyBase = 1 // ID de la devise par défaut HTG
                };

                _context.Transactions.Add(transactionRecord);
                await _context.SaveChangesAsync();

                // Puis débiter le wallet
                var now = DateTimeOffset.UtcNow;
                wallet.Balance -= amount;
                wallet.LastUpdated = now;
                wallet.UpdatedAt = now;
                _context.Wallets.Update(wallet);

                // Créer entrée de wallet transaction pour l'audit
                var walletTransaction = new WalletTransaction
                {
                    WalletId = wallet.Id,
                    UserId = userId,
                    Amount = -amount,
                    Balance = wallet.Balance,
                    CurrencyBase = wallet.CurrencyBase,
                    WalletType = walletType,
                    Type = WalletTransactionType.Withdrawal,
                    TransactionId = transactionRecord.Id.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Description = description ?? $"Retrait {method}"
                };

                _context.WalletTransactions.Add(walletTransaction);

                // Marquer la transaction comme complétée
                transactionRecord.Status = TransactionStatus.Completed;
                transactionRecord.CompletedAt = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync();

                // AUDIT OBLIGATOIRE : Enregistrer dans PlatformTransaction
                try
                {
                    // Calculer frais bancaires selon la méthode
                    decimal bankFees = CalculateBankFees(amount, method);
                    
                    await _auditService.RecordUserWithdrawalAsync(
                        userId, 
                        amount, 
                        bankFees, 
                        method.ToString(), 
                        transactionRecord.Id.ToString(),
                        description ?? $"Retrait utilisateur {method}"
                    );
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Erreur audit retrait User {UserId}, Transaction {TransactionId}", 
                        userId, transactionRecord.Id);
                    throw;
                    // Audit obligatoire: l'exception annule le retrait.
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Retrait traité: {Amount} pour {UserId}, Nouveau solde: {Balance}", 
                    amount, userId, wallet.Balance);

                return new FinancialOperationResult
                {
                    Success = true,
                    TransactionId = transactionRecord.Id.ToString(),
                    NewBalance = wallet.Balance,
                    OrderId = orderId?.ToString()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors du retrait pour {UserId}, Montant: {Amount}", userId, amount);
                
                return new FinancialOperationResult
                {
                    Success = false,
                    ErrorMessage = "Une erreur technique est survenue. Veuillez réessayer."
                };
            }
            });
        }

        public async Task<FinancialOperationResult> ProcessTransferAsync(string fromUserId, string toUserId, decimal amount, string description = null)
        {
            if (amount <= 0)
                return new FinancialOperationResult { Success = false, ErrorMessage = "Le montant doit être supérieur à zéro" };

            if (fromUserId == toUserId)
                return new FinancialOperationResult { Success = false, ErrorMessage = "Impossible de transférer vers soi-même" };

            // IMPORTANT: Utiliser ExecutionStrategy avec Npgsql pour permettre les retries
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
            {
                await CleanupExpiredBalanceReservationsAsync();

                // Obtenir les deux wallets avec ordre déterministe pour éviter deadlocks
                var userIds = new[] { fromUserId, toUserId }.OrderBy(x => x).ToArray();
                
                var wallets = await _context.Wallets
                    .Where(w => userIds.Contains(w.UserId) && w.Type == WalletType.Customer)
                    .OrderBy(w => w.UserId)
                    .ToListAsync();

                var fromWallet = wallets.FirstOrDefault(w => w.UserId == fromUserId);
                var toWallet = wallets.FirstOrDefault(w => w.UserId == toUserId);

                if (fromWallet == null || toWallet == null)
                {
                    return new FinancialOperationResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Portefeuille expéditeur ou destinataire non trouvé" 
                    };
                }

                // Vérifier solde disponible
                var reservedBalance = await GetReservedBalanceAsync(fromUserId, WalletType.Customer);
                var availableBalance = fromWallet.Balance - reservedBalance;

                if (availableBalance < amount)
                {
                    return new FinancialOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Solde insuffisant. Disponible: {availableBalance:C}, Demandé: {amount:C}"
                    };
                }

                var orderId = await GenerateUniqueOrderIdAsync();

                // Transaction sortante
                var outgoingTransaction = new Transaction
                {
                    UserId = fromUserId,
                    WalletId = fromWallet.Id,
                    WalletType = WalletType.Customer,
                    Amount = amount,
                    Type = TransactionType.Transfer,
                    Method = TransactionMethod.MCBusinessPay,
                    TransactionDate = DateTimeOffset.UtcNow,
                    Status = TransactionStatus.Completed,
                    OrderId = orderId?.ToString(),
                    Description = description ?? $"Transfert vers {toUserId}",
                    CurrencyBase = 1
                };

                // Transaction entrante
                var incomingTransaction = new Transaction
                {
                    UserId = toUserId,
                    WalletId = toWallet.Id,
                    WalletType = WalletType.Customer,
                    Amount = amount,
                    Type = TransactionType.Deposit,
                    Method = TransactionMethod.MCBusinessPay,
                    TransactionDate = DateTimeOffset.UtcNow,
                    Status = TransactionStatus.Completed,
                    OrderId = orderId?.ToString(),
                    Description = description ?? $"Transfert de {fromUserId}",
                    CurrencyBase = 1
                };

                _context.Transactions.AddRange(outgoingTransaction, incomingTransaction);
                await _context.SaveChangesAsync();

                // Mise à jour des soldes
                var now = DateTimeOffset.UtcNow;
                fromWallet.Balance -= amount;
                fromWallet.LastUpdated = now;
                fromWallet.UpdatedAt = now;
                toWallet.Balance += amount;
                toWallet.LastUpdated = now;
                toWallet.UpdatedAt = now;

                _context.Wallets.UpdateRange(fromWallet, toWallet);

                // Audit trail
                var fromWalletTransaction = new WalletTransaction
                {
                    WalletId = fromWallet.Id,
                    UserId = fromUserId,
                    Amount = -amount,
                    Balance = fromWallet.Balance,
                    CurrencyBase = fromWallet.CurrencyBase,
                    WalletType = WalletType.Customer,
                    Type = WalletTransactionType.Transfer,
                    TransactionId = outgoingTransaction.Id.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Description = description ?? $"Transfert vers {toUserId}"
                };

                var toWalletTransaction = new WalletTransaction
                {
                    WalletId = toWallet.Id,
                    UserId = toUserId,
                    Amount = amount,
                    Balance = toWallet.Balance,
                    CurrencyBase = toWallet.CurrencyBase,
                    WalletType = WalletType.Customer,
                    Type = WalletTransactionType.Deposit,
                    TransactionId = incomingTransaction.Id.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Description = description ?? $"Transfert de {fromUserId}"
                };

                _context.WalletTransactions.AddRange(fromWalletTransaction, toWalletTransaction);
                await _context.SaveChangesAsync();
                await _auditService.RecordUserTransferAsync(
                    fromUserId,
                    toUserId,
                    amount,
                    outgoingTransaction.Id.ToString(),
                    description ?? $"Transfert vers {toUserId}");
                await transaction.CommitAsync();

                try
                {
                    await _auditService.RecordUserTransferAsync(fromUserId, toUserId, amount, outgoingTransaction.Id.ToString(), description ?? $"Transfert vers {toUserId}");
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Erreur audit transfert {FromUserId} -> {ToUserId}, Transaction {TransactionId}", fromUserId, toUserId, outgoingTransaction.Id);
                }

                _logger.LogInformation("Transfert traité: {Amount} de {FromUserId} vers {ToUserId}", 
                    amount, fromUserId, toUserId);

                return new FinancialOperationResult
                {
                    Success = true,
                    TransactionId = outgoingTransaction.Id.ToString(),
                    NewBalance = fromWallet.Balance,
                    OrderId = orderId?.ToString()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors du transfert de {FromUserId} vers {ToUserId}, Montant: {Amount}", 
                    fromUserId, toUserId, amount);
                
                return new FinancialOperationResult
                {
                    Success = false,
                    ErrorMessage = "Une erreur technique est survenue. Veuillez réessayer."
                };
            }
            });
        }

        public async Task<FinancialOperationResult> ReverseWithdrawalAsync(
            string userId,
            string transactionId,
            string description = null)
        {
            if (!int.TryParse(transactionId, out var withdrawalId))
            {
                return new FinancialOperationResult { Success = false, ErrorMessage = "Identifiant de retrait invalide." };
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var withdrawal = await _context.Transactions.FirstOrDefaultAsync(t =>
                        t.Id == withdrawalId &&
                        t.UserId == userId &&
                        t.Type == TransactionType.Withdrawal);

                    if (withdrawal == null)
                    {
                        return new FinancialOperationResult { Success = false, ErrorMessage = "Retrait introuvable." };
                    }

                    var reversalExternalId = $"withdrawal_reversal_{withdrawal.Id}";
                    if (await _context.Transactions.AnyAsync(t => t.TransactionId == reversalExternalId))
                    {
                        var currentBalance = await _context.Wallets
                            .Where(w => w.UserId == userId && w.Type == withdrawal.WalletType)
                            .Select(w => w.Balance)
                            .FirstOrDefaultAsync();
                        return new FinancialOperationResult { Success = true, NewBalance = currentBalance };
                    }

                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && w.Type == withdrawal.WalletType);
                    if (wallet == null)
                    {
                        return new FinancialOperationResult { Success = false, ErrorMessage = "Portefeuille introuvable." };
                    }

                    wallet.Balance += withdrawal.Amount;
                    withdrawal.Status = TransactionStatus.Failed;

                    var reversal = new Transaction
                    {
                        UserId = userId,
                        WalletId = wallet.Id,
                        WalletType = withdrawal.WalletType,
                        Amount = withdrawal.Amount,
                        Type = TransactionType.Refund,
                        Method = withdrawal.Method,
                        Status = TransactionStatus.Completed,
                        TransactionId = reversalExternalId,
                        // Une annulation est une nouvelle écriture comptable. Elle ne doit pas
                        // réutiliser la référence unique envoyée au prestataire.
                        OrderId = null,
                        CurrencyBase = withdrawal.CurrencyBase,
                        Description = description ?? $"Annulation du retrait #{withdrawal.Id}",
                        CompletedAt = DateTimeOffset.UtcNow
                    };
                    _context.Transactions.Add(reversal);
                    await _context.SaveChangesAsync();

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        UserId = userId,
                        Amount = withdrawal.Amount,
                        Balance = wallet.Balance,
                        WalletType = withdrawal.WalletType,
                        Type = WalletTransactionType.Refund,
                        TransactionId = reversal.Id.ToString(),
                        CurrencyBase = wallet.CurrencyBase,
                        Description = reversal.Description
                    });

                    var bankFees = CalculateBankFees(withdrawal.Amount, withdrawal.Method);
                    await _auditService.RecordWithdrawalReversalAsync(
                        userId,
                        withdrawal.Amount,
                        bankFees,
                        withdrawal.Id.ToString(),
                        description);

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return new FinancialOperationResult
                    {
                        Success = true,
                        TransactionId = reversal.Id.ToString(),
                        NewBalance = wallet.Balance
                    };
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Erreur annulation retrait {TransactionId}", transactionId);
                    return new FinancialOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Le retrait a echoue et son remboursement necessite une verification."
                    };
                }
            });
        }

        public async Task<FinancialOperationResult> ProcessDepositAsync(string userId, decimal amount, TransactionMethod method, string description = null)
        {
            if (amount <= 0)
                return new FinancialOperationResult { Success = false, ErrorMessage = "Le montant doit être supérieur à zéro" };

            // IMPORTANT: Utiliser ExecutionStrategy avec Npgsql pour permettre les retries
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && w.Type == WalletType.Customer);
                
                if (wallet == null)
                {
                    // Créer wallet si n'existe pas
                    wallet = new Wallet
                    {
                        UserId = userId,
                        Type = WalletType.Customer,
                        Balance = 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        LastUpdated = DateTimeOffset.UtcNow
                    };
                    _context.Wallets.Add(wallet);
                    await _context.SaveChangesAsync();
                }

                var orderId = await GenerateUniqueOrderIdAsync();

                var transactionRecord = new Transaction
                {
                    UserId = userId,
                    WalletId = wallet.Id,
                    WalletType = WalletType.Customer,
                    Amount = amount,
                    Type = TransactionType.Deposit,
                    Method = method,
                    TransactionDate = DateTimeOffset.UtcNow,
                    Status = TransactionStatus.Completed,
                    OrderId = orderId?.ToString(),
                    Description = description ?? $"Dépôt {method}",
                    CurrencyBase = 1
                };

                _context.Transactions.Add(transactionRecord);
                await _context.SaveChangesAsync();

                var now = DateTimeOffset.UtcNow;
                wallet.Balance += amount;
                wallet.LastUpdated = now;
                wallet.UpdatedAt = now;
                _context.Wallets.Update(wallet);

                var walletTransaction = new WalletTransaction
                {
                    WalletId = wallet.Id,
                    UserId = userId,
                    Amount = amount,
                    Balance = wallet.Balance,
                    CurrencyBase = wallet.CurrencyBase,
                    WalletType = WalletType.Customer,
                    Type = WalletTransactionType.Deposit,
                    TransactionId = transactionRecord.Id.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Description = description ?? $"Dépôt {method}"
                };

                _context.WalletTransactions.Add(walletTransaction);
                await _context.SaveChangesAsync();

                // AUDIT OBLIGATOIRE : Enregistrer dans PlatformTransaction
                try
                {
                    await _auditService.RecordUserDepositAsync(
                        userId, 
                        amount, 
                        method.ToString(), 
                        transactionRecord.Id.ToString(),
                        description ?? $"Dépôt utilisateur {method}"
                    );
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Erreur audit dépôt User {UserId}, Transaction {TransactionId}", 
                        userId, transactionRecord.Id);
                    throw;
                    // Audit obligatoire: l'exception annule le depot.
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Dépôt traité: {Amount} pour {UserId}, Nouveau solde: {Balance}", 
                    amount, userId, wallet.Balance);

                return new FinancialOperationResult
                {
                    Success = true,
                    TransactionId = transactionRecord.Id.ToString(),
                    NewBalance = wallet.Balance,
                    OrderId = orderId?.ToString()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors du dépôt pour {UserId}, Montant: {Amount}", userId, amount);
                
                return new FinancialOperationResult
                {
                    Success = false,
                    ErrorMessage = "Une erreur technique est survenue. Veuillez réessayer."
                };
            }
            });
        }

        public async Task<FinancialOperationResult> CompletePendingDepositAsync(
            string orderId,
            string gatewayTransactionId,
            decimal confirmedAmount)
        {
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(gatewayTransactionId))
            {
                return new FinancialOperationResult { Success = false, ErrorMessage = "Reference de depot invalide." };
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var deposit = await _context.Transactions.FirstOrDefaultAsync(t =>
                        t.OrderId == orderId &&
                        t.Type == TransactionType.Deposit &&
                        t.Method == TransactionMethod.MonCash);

                    if (deposit == null)
                    {
                        return new FinancialOperationResult { Success = false, ErrorMessage = "Depot introuvable." };
                    }

                    if (deposit.Status == TransactionStatus.Completed)
                    {
                        if (!string.Equals(
                                deposit.TransactionId,
                                gatewayTransactionId,
                                StringComparison.Ordinal))
                        {
                            _logger.LogWarning(
                                "Tentative de confirmer le dépôt {OrderId} avec une autre transaction MonCash",
                                orderId);
                            return new FinancialOperationResult
                            {
                                Success = false,
                                ErrorMessage = "Ce dépôt est déjà associé à une autre transaction MonCash."
                            };
                        }

                        var existingBalance = await _context.Wallets
                            .Where(w => w.UserId == deposit.UserId && w.Type == WalletType.Customer)
                            .Select(w => w.Balance)
                            .FirstOrDefaultAsync();
                        return new FinancialOperationResult
                        {
                            Success = true,
                            TransactionId = deposit.Id.ToString(),
                            NewBalance = existingBalance,
                            OrderId = orderId
                        };
                    }

                    var expectedGrossAmount = MonCashPricing.CalculateDepositGross(deposit.Amount);
                    if (deposit.Status != TransactionStatus.Pending || confirmedAmount != expectedGrossAmount)
                    {
                        _logger.LogWarning(
                            "Montant MonCash inattendu pour {OrderId}. Attendu={ExpectedAmount}, Reçu={ConfirmedAmount}",
                            orderId,
                            expectedGrossAmount,
                            confirmedAmount);
                        return new FinancialOperationResult { Success = false, ErrorMessage = "Depot non payable ou montant confirme incorrect." };
                    }

                    var duplicateGatewayTransaction = await _context.Transactions.AnyAsync(t =>
                        t.TransactionId == gatewayTransactionId &&
                        t.Id != deposit.Id);
                    if (duplicateGatewayTransaction)
                    {
                        return new FinancialOperationResult { Success = false, ErrorMessage = "Transaction MonCash deja utilisee." };
                    }

                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == deposit.UserId && w.Type == WalletType.Customer);
                    if (wallet == null)
                    {
                        wallet = new Wallet
                        {
                            UserId = deposit.UserId,
                            Type = WalletType.Customer,
                            CurrencyBase = deposit.CurrencyBase,
                            Balance = 0m,
                            CreatedAt = DateTimeOffset.UtcNow,
                            LastUpdated = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                        _context.Wallets.Add(wallet);
                        await _context.SaveChangesAsync();
                    }

                    wallet.Balance += deposit.Amount;
                    deposit.Status = TransactionStatus.Completed;
                    deposit.TransactionId = gatewayTransactionId;
                    deposit.CompletedAt = DateTimeOffset.UtcNow;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wallet.Id,
                        UserId = deposit.UserId,
                        Amount = deposit.Amount,
                        Balance = wallet.Balance,
                        WalletType = WalletType.Customer,
                        Type = WalletTransactionType.Deposit,
                        TransactionId = deposit.Id.ToString(),
                        CurrencyBase = wallet.CurrencyBase,
                        Description = "Depot MonCash confirme"
                    });

                    await _auditService.RecordUserDepositAsync(
                        deposit.UserId,
                        deposit.Amount,
                        "MonCash",
                        gatewayTransactionId,
                        $"Depot MonCash confirme - Reference {orderId}");

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return new FinancialOperationResult
                    {
                        Success = true,
                        TransactionId = deposit.Id.ToString(),
                        NewBalance = wallet.Balance,
                        OrderId = orderId
                    };
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Erreur confirmation depot MonCash {OrderId}", orderId);
                    return new FinancialOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Le depot ne peut pas etre confirme pour le moment."
                    };
                }
            });
        }

        public async Task<decimal> GetAvailableBalanceAsync(string userId)
        {
            await CleanupExpiredBalanceReservationsAsync();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && w.Type == WalletType.Customer);
            if (wallet == null) return 0;

            var reservedBalance = await GetReservedBalanceAsync(userId, WalletType.Customer);
            return Math.Max(0, wallet.Balance - reservedBalance);
        }

        public async Task<bool> ReserveBalanceAsync(string userId, decimal amount, TimeSpan? reservationDuration = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && w.Type == WalletType.Customer);
                var availableBalance = await GetAvailableBalanceAsync(userId);
                if (availableBalance < amount) return false;

                var reservation = new BalanceReservation
                {
                    UserId = userId,
                    WalletType = WalletType.Customer,
                    WalletId = wallet?.Id,
                    Amount = amount,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(reservationDuration ?? _defaultReservationDuration),
                    Status = ReservationStatus.Active
                };

                _context.BalanceReservations.Add(reservation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> ConfirmBalanceReservationAsync(string userId, decimal amount)
        {
            var reservation = await _context.BalanceReservations
                .FirstOrDefaultAsync(br => br.UserId == userId &&
                                         br.WalletType == WalletType.Customer &&
                                         br.Amount == amount && 
                                         br.Status == ReservationStatus.Active &&
                                         br.ExpiresAt > DateTimeOffset.UtcNow);

            if (reservation == null) return false;

            reservation.Status = ReservationStatus.Confirmed;
            reservation.ConfirmedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task CleanupExpiredBalanceReservationsAsync()
        {
            var expired = await _context.BalanceReservations
                .Where(br => br.ExpiresAt <= DateTimeOffset.UtcNow && br.Status == ReservationStatus.Active)
                .ToListAsync();

            foreach (var reservation in expired)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.ReleasedAt = DateTimeOffset.UtcNow;
            }

            if (expired.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task<decimal> GetReservedBalanceAsync(string userId, WalletType walletType)
        {
            return await _context.BalanceReservations
                .Where(br => br.UserId == userId && 
                           br.WalletType == walletType &&
                           br.Status == ReservationStatus.Active &&
                           br.ExpiresAt > DateTimeOffset.UtcNow)
                .SumAsync(br => br.Amount);
        }

        /// <summary>
        /// Calcule les frais bancaires selon la méthode de retrait
        /// CONFIGURÉ : Peut être personnalisé par méthode de paiement
        /// </summary>
        private decimal CalculateBankFees(decimal amount, TransactionMethod method)
        {
            return method switch
            {
                TransactionMethod.BankTransfer => Math.Max(25m, amount * 0.015m), // Min 25 HTG ou 1.5%
                TransactionMethod.MonCash => amount * 0.02m, // 2%
                TransactionMethod.CreditCard => amount * 0.035m, // 3.5%
                TransactionMethod.Stripe => Math.Max(15m, amount * 0.029m), // Min 15 HTG ou 2.9%
                TransactionMethod.PayPal => amount * 0.034m, // 3.4%
                TransactionMethod.Cash => 0m, // Pas de frais pour espèces
                _ => amount * 0.02m // Défaut 2%
            };
        }

        private async Task<string> GenerateUniqueOrderIdAsync()
        {
            string orderId;
            do
            {
                orderId = Guid.NewGuid().ToString("N")[..12].ToUpper();
            } while (await _context.Transactions.AnyAsync(t => t.OrderId == orderId));

            return orderId;
        }
    }

    // Modèles supportant le service
    public class FinancialOperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string TransactionId { get; set; }
        public decimal NewBalance { get; set; }
        public string OrderId { get; set; }
    }

    // Modèle défini dans Models/StockReservation.cs (BalanceReservation)
}
