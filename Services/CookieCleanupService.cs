using MCBusiness.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Services
{
    public class CookieCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CookieCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromDays(1); // Nettoyer tous les jours

        public CookieCleanupService(
            IServiceProvider serviceProvider,
            ILogger<CookieCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service de nettoyage des cookies démarré");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cookieConsentService = scope.ServiceProvider.GetRequiredService<ICookieConsentService>();
                    
                    await cookieConsentService.CleanupOldConsentsAsync();
                    
                    _logger.LogInformation("Nettoyage des anciens consentements cookies effectué");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors du nettoyage des consentements cookies");
                }

                // Attendre le prochain intervalle de nettoyage
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
    }
}
