using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service universel pour la gestion des points de fidélité
    /// INDÉPENDANT du système d'argent (AmbassadorCommission)
    /// </summary>
    public class PointTransactionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PointTransactionService> _logger;

        public PointTransactionService(
            AppDbContext context,
            ILogger<PointTransactionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region ATTRIBUTION DE POINTS

        /// <summary>
        /// Attribue des points à un utilisateur
        /// </summary>
        public async Task<bool> AwardPointsAsync(
            string userId, 
            int pointsAmount, 
            PointTransactionType type, 
            string description,
            string? details = null,
            int? relatedId = null,
            string? adminUserId = null)
        {
            try
            {
                if (pointsAmount <= 0)
                {
                    _logger.LogWarning($"Invalid points amount: {pointsAmount} for user {userId}");
                    return false;
                }

                // Vérifier que l'utilisateur existe
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {userId}");
                    return false;
                }

                // Créer la transaction
                var pointTransaction = new PointTransaction
                {
                    UserId = userId,
                    PointsAmount = pointsAmount,
                    Type = type,
                    Description = description,
                    Details = details,
                    AdminUserId = adminUserId,
                    Status = PointTransactionStatus.Completed,
                    TransactionDate = DateTimeOffset.UtcNow
                };

                // Attribuer les références selon le type
                switch (type)
                {
                    case PointTransactionType.ReferralBonus:
                    case PointTransactionType.WelcomeBonus:
                        pointTransaction.ReferralId = relatedId;
                        break;
                    case PointTransactionType.PurchaseBonus:
                        pointTransaction.OrderId = relatedId;
                        break;
                }

                // Utiliser une transaction pour garantir la cohérence
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Mettre à jour le solde utilisateur AVANT d'ajouter la transaction
                    user.TotalPoints += pointsAmount;
                    pointTransaction.BalanceAfterTransaction = user.TotalPoints;

                    // Ajouter la transaction
                    _context.PointTransactions.Add(pointTransaction);
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Points awarded: {pointsAmount} to user {userId} - Type: {type} - New balance: {user.TotalPoints}");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error in transaction for awarding points to user {userId}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error awarding points to user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Consomme des points pour une récompense
        /// </summary>
        public async Task<bool> ConsumePointsAsync(
            string userId, 
            int pointsAmount, 
            PointTransactionType type, 
            string description,
            string? details = null)
        {
            try
            {
                if (pointsAmount <= 0)
                {
                    _logger.LogWarning($"Invalid points amount for consumption: {pointsAmount} for user {userId}");
                    return false;
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {userId}");
                    return false;
                }

                // Vérifier que l'utilisateur a suffisamment de points
                if (user.TotalPoints < pointsAmount)
                {
                    _logger.LogWarning($"Insufficient points for user {userId}: has {user.TotalPoints}, needs {pointsAmount}");
                    return false;
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Déduire les points
                    user.TotalPoints -= pointsAmount;

                    // Créer la transaction (montant négatif pour consommation)
                    var pointTransaction = new PointTransaction
                    {
                        UserId = userId,
                        PointsAmount = -pointsAmount,
                        Type = type,
                        Description = description,
                        Details = details,
                        Status = PointTransactionStatus.Completed,
                        TransactionDate = DateTimeOffset.UtcNow,
                        BalanceAfterTransaction = user.TotalPoints
                    };

                    _context.PointTransactions.Add(pointTransaction);
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Points consumed: {pointsAmount} from user {userId} - Type: {type} - New balance: {user.TotalPoints}");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error in transaction for consuming points from user {userId}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error consuming points from user {userId}");
                return false;
            }
        }

        #endregion

        #region POINTS DE PARRAINAGE

        /// <summary>
        /// Attribue des points de parrainage selon le plan ambassadeur
        /// </summary>
        public async Task<bool> ProcessReferralPointsAsync(Referral referral)
        {
            try
            {
                // Récupérer le plan de l'ambassadeur
                var ambassadorPlan = await GetAmbassadorPlanAsync(referral.ReferrerId);
                if (ambassadorPlan == null)
                {
                    _logger.LogDebug($"No ambassador plan found for user {referral.ReferrerId}");
                    return true; // Pas d'erreur, juste pas de points
                }

                // Calculer les points selon la génération et le plan
                var pointsAmount = CalculateReferralPoints(referral.Generation, ambassadorPlan);
                
                if (pointsAmount <= 0)
                {
                    _logger.LogDebug($"No points calculated for generation {referral.Generation}");
                    return true;
                }

                // Attribuer les points
                return await AwardPointsAsync(
                    referral.ReferrerId,
                    pointsAmount,
                    PointTransactionType.ReferralBonus,
                    $"Bonus parrainage génération {referral.Generation}",
                    $"Filleul: {referral.Referred?.UserName}, Plan: {ambassadorPlan.Name}",
                    referral.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing referral points for referral {referral.Id}");
                return false;
            }
        }

        /// <summary>
        /// Calcule les points de parrainage selon le plan
        /// </summary>
        private int CalculateReferralPoints(int generation, AmbassadorPlan plan)
        {
            return generation switch
            {
                1 => plan.PointsGeneration1,
                2 => plan.PointsGeneration2,
                3 => plan.PointsGeneration3,
                _ => 0
            };
        }

        #endregion

        #region HELPERS

        /// <summary>
        /// Récupère le plan ambassadeur actif d'un utilisateur
        /// </summary>
        private async Task<AmbassadorPlan?> GetAmbassadorPlanAsync(string userId)
        {
            return await _context.AmbassadorSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && 
                           s.Status == SubscriptionStatus.Active && 
                           s.EndDate > DateTimeOffset.UtcNow)
                .Select(s => s.Plan)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Obtient l'historique des points d'un utilisateur
        /// </summary>
        public async Task<List<PointTransaction>> GetUserPointHistoryAsync(string userId, int limit = 50)
        {
            return await _context.PointTransactions
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.TransactionDate)
                .Take(limit)
                .ToListAsync();
        }

        /// <summary>
        /// Calcule le solde actuel des points (vérification)
        /// </summary>
        public async Task<int> CalculateCurrentPointBalanceAsync(string userId)
        {
            return await _context.PointTransactions
                .Where(pt => pt.UserId == userId && pt.Status == PointTransactionStatus.Completed)
                .SumAsync(pt => pt.PointsAmount);
        }

        /// <summary>
        /// Synchronise le solde utilisateur avec les transactions (maintenance)
        /// </summary>
        public async Task<bool> SynchronizeUserPointBalanceAsync(string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                var calculatedBalance = await CalculateCurrentPointBalanceAsync(userId);
                
                if (user.TotalPoints != calculatedBalance)
                {
                    _logger.LogWarning($"Point balance mismatch for user {userId}: stored={user.TotalPoints}, calculated={calculatedBalance}");
                    user.TotalPoints = calculatedBalance;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Point balance synchronized for user {userId}: {calculatedBalance}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error synchronizing point balance for user {userId}");
                return false;
            }
        }

        #endregion
    }
}
