using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;

public class TransactionStatusUpdater : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TransactionStatusUpdater> _logger;

    public TransactionStatusUpdater(IServiceScopeFactory serviceScopeFactory, 
                                    ILogger<TransactionStatusUpdater> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }
    public MonCashService.PaymentDetails PaymentDetails { get; set; }
    public string StatusMessage { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TransactionStatusUpdater démarré.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var monCashService = scope.ServiceProvider.GetRequiredService<MonCashService>();
                    var secureFinancialService = scope.ServiceProvider.GetRequiredService<MCBusiness.Services.ISecureFinancialService>();

                    var pendingTransactions = await dbContext.Transactions
                        .Where(t => t.Status == TransactionStatus.Pending && 
                                    t.Method == TransactionMethod.MonCash && 
                                    t.Type == TransactionType.Deposit)
                        .ToListAsync(stoppingToken);

                    foreach (var transaction in pendingTransactions)
                    {
                        try
                        {
                            _logger.LogInformation($"Traitement de la transaction {transaction.OrderId} en cours...");

                            try
                            {
                                PaymentDetails = await monCashService.RetrievePaymentByOrderIdAsync(transaction.OrderId);

                                if (PaymentDetails != null && PaymentDetails.Payment != null)
                                {
                                    var currentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == transaction.UserId);

                                    if (currentUser == null)
                                    {
                                        _logger.LogWarning($"Utilisateur introuvable pour la transaction {transaction.OrderId}.");
                                        continue;
                                    }

                                    if (PaymentDetails.Status == StatusCodes.Status200OK &&
                                        string.Equals(PaymentDetails.Payment.Message, "successful", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogInformation($"Transaction {transaction.OrderId} réussie.");
                                        var settlement = await secureFinancialService.CompletePendingDepositAsync(
                                            transaction.OrderId,
                                            PaymentDetails.Payment.TransactionId,
                                            PaymentDetails.Payment.Cost);

                                        if (!settlement.Success)
                                        {
                                            _logger.LogWarning(
                                                "Depot {OrderId} non confirme: {Error}",
                                                transaction.OrderId,
                                                settlement.ErrorMessage);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "Transaction MonCash {OrderId} non confirmée; elle reste en attente de rapprochement",
                                            transaction.OrderId);
                                    }
                                }
                                else
                                {
                                    _logger.LogError($"Détails du paiement introuvables pour la transaction {transaction.OrderId}.");
                                }
                            }
                            catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                _logger.LogWarning($"Transaction {transaction.OrderId} non trouvée dans l'API MonCash (404 Not Found).");
                                continue;
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Erreur transaction {transaction.OrderId} : {ex.Message}");
                            continue;
                        }
                    }

                    // Une absence de confirmation n'est pas un échec. Les transactions anciennes
                    // restent rapprochables afin qu'un paiement tardif puisse être crédité.
                    var outdatedTransactions = await dbContext.Transactions
                        .Where(t => t.Status == TransactionStatus.Pending && 
                                    t.Method == TransactionMethod.MonCash && 
                                    t.Type == TransactionType.Deposit &&
                                    t.TransactionDate <= DateTimeOffset.UtcNow.AddHours(-1)) // Plus de 1h
                        .ToListAsync(stoppingToken);

                    if (outdatedTransactions.Any())
                    {
                        foreach (var transaction in outdatedTransactions)
                        {
                            _logger.LogWarning(
                                "Transaction MonCash {OrderId} toujours en attente après plus d'une heure",
                                transaction.OrderId);
                        }
                    }

                    // Réconcilier les payouts dont la réponse initiale était ambiguë.
                    // Aucun remboursement automatique n'est effectué sur un timeout ou un 404.
                    var processingWithdrawals = await dbContext.Transactions
                        .Where(t => t.Status == TransactionStatus.Processing &&
                                    t.Method == TransactionMethod.MonCash &&
                                    t.Type == TransactionType.Withdrawal &&
                                    t.OrderId != null)
                        .ToListAsync(stoppingToken);

                    foreach (var withdrawal in processingWithdrawals)
                    {
                        try
                        {
                            var payoutStatus = await monCashService
                                .CheckTransactionStatusMonCashAsync(withdrawal.OrderId!);

                            if (payoutStatus.Status == StatusCodes.Status200OK &&
                                string.Equals(
                                    payoutStatus.TransStatus,
                                    "successful",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                withdrawal.Status = TransactionStatus.Completed;
                                withdrawal.CompletedAt = DateTimeOffset.UtcNow;
                                _logger.LogInformation(
                                    "Retrait MonCash {OrderId} confirmé par rapprochement",
                                    withdrawal.OrderId);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "Retrait MonCash {OrderId} toujours en cours de vérification",
                                    withdrawal.OrderId);
                            }
                        }
                        catch (HttpRequestException httpEx) when (
                            httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning(
                                "Retrait MonCash {OrderId} absent temporairement du rapprochement; aucun remboursement automatique",
                                withdrawal.OrderId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Échec du rapprochement du retrait MonCash {OrderId}",
                                withdrawal.OrderId);
                        }
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur globale : {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

}
