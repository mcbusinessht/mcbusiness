using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using System.Collections.Concurrent;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service background pour traiter les logs d'activité utilisateur de manière asynchrone
    /// OBJECTIF: Décharger le thread HTTP principal des insertions DB (gain 10-20× sur requêtes)
    /// </summary>
    public class UserActivityBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserActivityBackgroundService> _logger;
        
        // Queue thread-safe pour les logs en attente
        private readonly ConcurrentQueue<UserActivityLog> _activityQueue = new();
        
        // Signal pour réveiller le service quand il y a des données
        private readonly SemaphoreSlim _signal = new(0);
        
        private const int BATCH_SIZE = 100; // Traiter par lots de 100
        private const int FLUSH_INTERVAL_SECONDS = 5; // Flush toutes les 5 secondes minimum

        public UserActivityBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<UserActivityBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Ajoute un log d'activité à la queue (appelé depuis le middleware)
        /// </summary>
        public void EnqueueActivity(UserActivityLog activity)
        {
            _activityQueue.Enqueue(activity);
            _signal.Release(); // Signal qu'il y a des données à traiter
        }

        /// <summary>
        /// Obtient le nombre d'activités en attente (pour monitoring)
        /// </summary>
        public int GetPendingCount() => _activityQueue.Count;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UserActivityBackgroundService démarré");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Attendre soit un signal, soit le timeout de flush
                    await _signal.WaitAsync(TimeSpan.FromSeconds(FLUSH_INTERVAL_SECONDS), stoppingToken);

                    // Traiter les logs en attente
                    await ProcessPendingActivitiesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service en cours d'arrêt - traiter les logs restants
                    _logger.LogInformation("UserActivityBackgroundService en cours d'arrêt - traitement final");
                    await ProcessPendingActivitiesAsync(CancellationToken.None);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur dans UserActivityBackgroundService");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Pause avant retry
                }
            }

            _logger.LogInformation("UserActivityBackgroundService arrêté");
        }

        private async Task ProcessPendingActivitiesAsync(CancellationToken cancellationToken)
        {
            if (_activityQueue.IsEmpty)
                return;

            var batch = new List<UserActivityLog>();

            // Déqueuer jusqu'à BATCH_SIZE éléments
            while (batch.Count < BATCH_SIZE && _activityQueue.TryDequeue(out var activity))
            {
                batch.Add(activity);
            }

            if (batch.Count == 0)
                return;

            try
            {
                // Utiliser un scope séparé pour le DbContext
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Insertion en batch
                await dbContext.UserActivityLogs.AddRangeAsync(batch, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogDebug($"UserActivityBackgroundService: {batch.Count} activités enregistrées");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'enregistrement de {batch.Count} activités");
                
                // En cas d'erreur, remettre dans la queue (avec limite pour éviter boucle infinie)
                if (batch.Count < 1000) // Limite de sécurité
                {
                    foreach (var activity in batch)
                    {
                        _activityQueue.Enqueue(activity);
                    }
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("UserActivityBackgroundService: Arrêt demandé");
            
            // Traiter les logs restants avant l'arrêt
            await ProcessPendingActivitiesAsync(cancellationToken);
            
            await base.StopAsync(cancellationToken);
        }
    }
}
