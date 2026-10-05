using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{
    public class SellerPaymentReconciliationProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SellerPaymentReconciliationProcessor> _logger;
        private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(15);

        public SellerPaymentReconciliationProcessor(
            IServiceProvider serviceProvider,
            ILogger<SellerPaymentReconciliationProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SellerPaymentReconciliationProcessor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var sellerPaymentService = scope.ServiceProvider.GetRequiredService<ISellerPaymentService>();
                    var reconciled = await sellerPaymentService.ReconcileDeliveredSellerPaymentsAsync();

                    if (reconciled > 0)
                    {
                        _logger.LogWarning(
                            "Seller payment reconciliation repaired {Count} delivered order/company financial records",
                            reconciled);
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error during seller payment reconciliation cycle");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during seller payment reconciliation cycle");
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

            _logger.LogInformation("SellerPaymentReconciliationProcessor stopped");
        }
    }
}
