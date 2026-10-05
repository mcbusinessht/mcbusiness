using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Services
{
    public class CancelOrderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CancelOrderService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public CancelOrderService(
            IServiceScopeFactory scopeFactory,
            ILogger<CancelOrderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CancelOrderService démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CancelStaleOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de l'annulation automatique des commandes.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("CancelOrderService arrêté.");
        }

        private async Task CancelStaleOrdersAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-60);

            // Récupère toutes les commandes "Pending" créées il y a plus de 60min
            var staleOrders = await db.Orders
                .Where(o => o.Status == OrderStatus.Pending
                            && o.OrderDate <= cutoff
                            && !o.IsDeleted)
                .ToListAsync(token);

            if (!staleOrders.Any())
            {
                _logger.LogDebug("Aucune commande à annuler à {Time}", DateTimeOffset.UtcNow);
                return;
            }

            _logger.LogInformation("{Count} commande(s) trouvée(s) en Pending >60min. Annulation en cours...", staleOrders.Count);

            foreach (var order in staleOrders)
            {
                order.Status = OrderStatus.Cancelled;
                order.IsDeleted = false; // facultatif, selon ta logique soft-delete
                // Eventuellement : order.DeletedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(token);

            _logger.LogInformation("{Count} commande(s) annulée(s) automatiquement.", staleOrders.Count);
        }
    }
}
