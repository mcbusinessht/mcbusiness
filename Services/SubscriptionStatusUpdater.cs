using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MCBusiness.Models;
using MCBusiness.Services;
using MCBusiness.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.IO;
using System.Data;

public class SubscriptionStatusUpdater : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSender _emailSender;
    private Timer _timer;

    public SubscriptionStatusUpdater(IServiceScopeFactory scopeFactory, IEmailSender emailSender)
    {
        _emailSender = emailSender;
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Démarrer le timer pour exécuter la logique périodiquement
        _timer = new Timer(UpdateStatuses, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private async void UpdateStatuses(object state)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditService = scope.ServiceProvider.GetRequiredService<IPlatformTransactionAuditService>();

            // Trouver les abonnements expirés et mettre à jour leur Status
            var subscriptionsToUpdate = context.AmbassadorSubscriptions
                .Include(p => p.Plan)
                .Where(s => s.EndDate <= DateTimeOffset.UtcNow && s.Status == SubscriptionStatus.Active)
                .ToList();

            foreach (var subscription in subscriptionsToUpdate)
            {
                subscription.Status = SubscriptionStatus.Inactive;

                try
                {
                    var user = context.Users.FirstOrDefault(u => u.Id == subscription.UserId);
                    if (user != null)
                    {
                        var walletTransactionRefund = await context.WalletTransactions.FirstOrDefaultAsync(w => w.UserId == user.Id && w.Type == WalletTransactionType.Refund && w.WalletType == WalletType.Customer);
                        var walletTransactionBonus = await context.WalletTransactions.FirstOrDefaultAsync(w => w.UserId == user.Id && w.Type == WalletTransactionType.Bonus && w.WalletType == WalletType.Earnings);

                        if (walletTransactionRefund != null && walletTransactionBonus != null)
                        {
                            if (walletTransactionRefund.Balance > 0)
                            {
                                var refundResult = walletTransactionRefund.Balance;
                                await using var dbTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                                try
                                {
                                    var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserId == user.Id && w.Type == WalletType.Customer);
                                    if (wallet == null)
                                    {
                                        throw new InvalidOperationException($"Portefeuille introuvable pour l'utilisateur {user.Id}.");
                                    }

                                    await auditService.RecordSubscriptionRefundAsync(
                                        user.Id,
                                        refundResult,
                                        subscription.Id,
                                        $"Remboursement pour l'abonnement {subscription.Plan.Name}");

                                    wallet.Balance += refundResult;
                                    wallet.LastUpdated = DateTimeOffset.UtcNow;
                                    walletTransactionRefund.Balance = 0;
                                    walletTransactionBonus.Balance = 0;

                                    var transactionId = $"subscription_refund_{subscription.Id}";
                                    context.Transactions.Add(new Transaction
                                    {
                                        UserId = user.Id,
                                        WalletId = wallet.Id,
                                        WalletType = WalletType.Customer,
                                        Amount = refundResult,
                                        CurrencyBase = wallet.CurrencyBase,
                                        Type = TransactionType.Refund,
                                        Method = TransactionMethod.MCBusinessPay,
                                        Description = $"Remboursement pour l'abonnement {subscription.Plan.Name}",
                                        Status = TransactionStatus.Completed,
                                        TransactionId = transactionId,
                                        CompletedAt = DateTimeOffset.UtcNow
                                    });

                                    context.WalletTransactions.Add(new WalletTransaction
                                    {
                                        WalletId = wallet.Id,
                                        UserId = user.Id,
                                        Amount = refundResult,
                                        Balance = wallet.Balance,
                                        CurrencyBase = wallet.CurrencyBase,
                                        WalletType = WalletType.Customer,
                                        Type = WalletTransactionType.Refund,
                                        TransactionId = transactionId,
                                        Description = $"Crédit du remboursement de l'abonnement #{subscription.Id}"
                                    });

                                    await context.SaveChangesAsync();
                                    await dbTransaction.CommitAsync();
                                }
                                catch
                                {
                                    await dbTransaction.RollbackAsync();
                                    throw;
                                }
                            }
                            else
                            {
                                walletTransactionRefund.Balance = 0;
                                walletTransactionBonus.Balance = 0;
                                context.WalletTransactions.Update(walletTransactionRefund);
                                context.WalletTransactions.Update(walletTransactionBonus);
                                await context.SaveChangesAsync();
                            }
                        }

                        var html_subscription_expired = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "subscription_expired.html"));
                        // Remplacement des variables
                        html_subscription_expired = html_subscription_expired.Replace("{{FullName}}", user.UserName)
                                                                             .Replace("{{PlanName}}", subscription.Plan.Name);
                        await _emailSender.SendEmailAsync(user.Email, "Abonnement Expiré", html_subscription_expired);

                        // Retirer le rôle "Ambassador" après l'expiration de la subscription
                        await RemoveAmbassadorRoleAsync(scope, user);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de l'envoi de l'email : {ex.Message}");
                }
            }

            context.SaveChanges();
        }
    }

    /// <summary>
    /// Retire le rôle "Ambassador" si l'utilisateur n'a pas d'autres subscriptions actives
    /// </summary>
    private async Task RemoveAmbassadorRoleAsync(IServiceScope scope, ApplicationUser user)
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Vérifier si l'utilisateur a d'autres subscriptions actives
            bool hasActiveSubscription = context.AmbassadorSubscriptions
                .Any(s => s.UserId == user.Id && s.Status == SubscriptionStatus.Active);

            if (!hasActiveSubscription)
            {
                // Vérifier si l'utilisateur a le rôle Ambassador
                bool isAmbassador = await userManager.IsInRoleAsync(user, "Ambassador");
                if (isAmbassador)
                {
                    // Retirer le rôle Ambassador
                    await userManager.RemoveFromRoleAsync(user, "Ambassador");
                    Console.WriteLine($"Utilisateur {user.UserName} retiré du rôle Ambassador (pas de subscription active).");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors du retrait du rôle Ambassador : {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Arrêter le timer
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
