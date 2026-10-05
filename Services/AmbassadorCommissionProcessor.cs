using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    /// <summary>
    /// Background Service pour traitement automatique des commissions ambassadeur
    /// Sépare la logique de traitement asynchrone du calcul métier
    /// </summary>
    public class AmbassadorCommissionProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AmbassadorCommissionProcessor> _logger;
        private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(30); // Configurable

        public AmbassadorCommissionProcessor(
            IServiceProvider serviceProvider,
            ILogger<AmbassadorCommissionProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AmbassadorCommissionProcessor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var commissionService = scope.ServiceProvider.GetRequiredService<AmbassadorCommissionService>();

                    // 1. Traiter nouvelles commandes livrées
                    await ProcessNewDeliveredOrdersAsync(context, commissionService);

                    // 2. Libérer commissions après délai
                    await commissionService.ProcessPendingCommissionsAsync();

                    // 3. Nettoyer les commissions expirées (optionnel)
                    await CleanupExpiredCommissionsAsync(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in AmbassadorCommissionProcessor cycle");
                }

                try
                {
                    await Task.Delay(_processingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("AmbassadorCommissionProcessor stopped");
        }

        /// <summary>
        /// Traite les commandes livrées qui n'ont pas encore de commissions
        /// </summary>
        private async Task ProcessNewDeliveredOrdersAsync(AppDbContext context, AmbassadorCommissionService commissionService)
        {
            var ordersToProcess = await context.Orders
                .Where(o => o.Status == OrderStatus.Delivered
                         && !string.IsNullOrEmpty(o.ReferredByAmbassador)
                         && !context.AmbassadorCommissions.Any(c => c.OrderId == o.Id))
                .Take(50) // Limite pour éviter la surcharge
                .ToListAsync();

            if (!ordersToProcess.Any()) return;

            _logger.LogInformation($"Processing {ordersToProcess.Count} delivered orders for commissions");

            foreach (var order in ordersToProcess)
            {
                try
                {
                    var commissions = await commissionService.CalculateCommissionsAsync(order);
                    
                    foreach (var commission in commissions)
                    {
                        context.AmbassadorCommissions.Add(commission);
                    }

                    _logger.LogDebug($"Created {commissions.Count} commissions for order {order.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing order {order.Id} for commissions");
                }
            }

            if (ordersToProcess.Any())
            {
                await context.SaveChangesAsync();
                _logger.LogInformation($"Successfully processed {ordersToProcess.Count} orders");
            }
        }

        /// <summary>
        /// Nettoie les commissions rejetées anciennes (optionnel)
        /// </summary>
        private async Task CleanupExpiredCommissionsAsync(AppDbContext context)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-90); // Garder 90 jours

            var expiredCommissions = await context.AmbassadorCommissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Rejected
                         && c.CreatedAt < cutoffDate)
                .Take(100)
                .ToListAsync();

            if (expiredCommissions.Any())
            {
                context.AmbassadorCommissions.RemoveRange(expiredCommissions);
                await context.SaveChangesAsync();
                
                _logger.LogInformation($"Cleaned up {expiredCommissions.Count} expired rejected commissions");
            }
        }
    }
}
