using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // Nettoyage toutes les 5 minutes

        public CleanupService(IServiceProvider serviceProvider, ILogger<CleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service en cours d'arrêt
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors du nettoyage automatique");

                    // Ne pas faire échouer le host lors de l'arrêt (token annulé)
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Attendre avant de réessayer
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task PerformCleanupAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var stockReservationService = scope.ServiceProvider.GetRequiredService<IStockReservationService>();
            var financialService = scope.ServiceProvider.GetRequiredService<ISecureFinancialService>();

            try
            {
                // Nettoyer les réservations de stock expirées
                await stockReservationService.CleanupExpiredReservationsAsync();

                // Nettoyer les réservations de balance expirées
                await financialService.CleanupExpiredBalanceReservationsAsync();

                _logger.LogDebug("Nettoyage automatique effectué avec succès");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du nettoyage des réservations");
            }

            // Nettoyer d'autres éléments selon les besoins
            try
            {
                using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await CleanupOldSessionData(context);
                await CleanupExpiredNotifications(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du nettoyage général");
            }
        }

        private async Task CleanupOldSessionData(AppDbContext context)
        {
            // Nettoyer les anciennes données de session (si vous en avez)
            // Par exemple, supprimer les paniers abandonnés depuis plus de 30 jours
            var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);

            var abandonedCarts = context.Carts
                .Where(c => c.Items.All(i => i.UpdatedAt < thirtyDaysAgo))
                .ToList();

            if (abandonedCarts.Any())
            {
                context.Carts.RemoveRange(abandonedCarts);
                await context.SaveChangesAsync();
                _logger.LogInformation($"Supprimé {abandonedCarts.Count} paniers abandonnés");
            }
        }

        private async Task CleanupExpiredNotifications(AppDbContext context)
        {
            // Supprimer les notifications lues de plus de 90 jours
            var ninetyDaysAgo = DateTimeOffset.UtcNow.AddDays(-90);

            var expiredNotifications = context.Notifications
                .Where(n => n.IsRead && n.CreatedAt < ninetyDaysAgo)
                .ToList();

            if (expiredNotifications.Any())
            {
                context.Notifications.RemoveRange(expiredNotifications);
                await context.SaveChangesAsync();
                _logger.LogInformation("Supprimé {Count} anciennes notifications", expiredNotifications.Count);
            }
        }
    }
}
