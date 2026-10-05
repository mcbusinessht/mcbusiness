using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service centralisé pour gérer l'accès aux fonctionnalités ambassador
    /// 
    /// Logique:
    /// - Vérifier si l'utilisateur a le rôle "Ambassador"
    /// - Vérifier si l'utilisateur a une subscription ACTIVE
    /// - Bloquer/autoriser l'accès à une fonctionnalité en conséquence
    /// 
    /// Ne JAMAIS faire confiance au frontend seul!
    /// </summary>
    public interface IAmbassadorAccessService
    {
        /// <summary>
        /// Vérifie si un utilisateur peut accéder aux fonctionnalités ambassador
        /// </summary>
        Task<bool> CanAccessAmbassadorFeaturesAsync(string userId);

        /// <summary>
        /// Vérifie si un utilisateur peut effectuer des retraits
        /// </summary>
        Task<bool> CanWithdrawAsync(string userId);

        /// <summary>
        /// Vérifie si un utilisateur peut gérer son plan
        /// </summary>
        Task<bool> CanManagePlanAsync(string userId);

        /// <summary>
        /// Vérifie si un utilisateur peut voir les statistiques avancées
        /// </summary>
        Task<bool> CanViewAdvancedStatsAsync(string userId);

        /// <summary>
        /// Obtient les infos de subscription de l'utilisateur
        /// </summary>
        Task<AmbassadorSubscription> GetActiveSubscriptionAsync(string userId);

        /// <summary>
        /// Vérifie si une action sensible est autorisée
        /// </summary>
        Task<AccessCheckResult> CheckAccessAsync(string userId, AmbassadorAction action);
    }

    public class AmbassadorAccessService : IAmbassadorAccessService
    {
        private readonly AppDbContext _context;

        public AmbassadorAccessService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Vérification principale : subscription ACTIVE + rôle Ambassador
        /// </summary>
        public async Task<bool> CanAccessAmbassadorFeaturesAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var subscription = await _context.AmbassadorSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.EndDate > DateTimeOffset.UtcNow
                    && !s.IsDeleted);

            return subscription != null;
        }

        /// <summary>
        /// Vérifier retrait: subscription active uniquement
        /// </summary>
        public async Task<bool> CanWithdrawAsync(string userId)
        {
            // Vérifier simplement la subscription active
            return await CanAccessAmbassadorFeaturesAsync(userId);
        }

        /// <summary>
        /// Vérifier gestion plan: subscription active uniquement
        /// </summary>
        public async Task<bool> CanManagePlanAsync(string userId)
        {
            return await CanAccessAmbassadorFeaturesAsync(userId);
        }

        /// <summary>
        /// Vérifier stats avancées: subscription active uniquement
        /// </summary>
        public async Task<bool> CanViewAdvancedStatsAsync(string userId)
        {
            return await CanAccessAmbassadorFeaturesAsync(userId);
        }

        /// <summary>
        /// Récupérer la subscription active
        /// </summary>
        public async Task<AmbassadorSubscription> GetActiveSubscriptionAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            return await _context.AmbassadorSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.EndDate > DateTimeOffset.UtcNow
                    && !s.IsDeleted);
        }

        /// <summary>
        /// Vérification générique pour une action
        /// </summary>
        public async Task<AccessCheckResult> CheckAccessAsync(string userId, AmbassadorAction action)
        {
            var result = new AccessCheckResult { Action = action };

            if (string.IsNullOrEmpty(userId))
            {
                result.IsAllowed = false;
                result.Reason = "Utilisateur non authentifié";
                return result;
            }

            var hasActiveSubscription = await CanAccessAmbassadorFeaturesAsync(userId);

            return action switch
            {
                AmbassadorAction.ViewWallet => new AccessCheckResult
                {
                    IsAllowed = hasActiveSubscription,
                    Action = action,
                    Reason = hasActiveSubscription ? null : "Abonnement requis"
                },

                AmbassadorAction.ViewCommissions => new AccessCheckResult
                {
                    IsAllowed = hasActiveSubscription,
                    Action = action,
                    Reason = hasActiveSubscription ? null : "Abonnement requis"
                },

                AmbassadorAction.Withdraw => new AccessCheckResult
                {
                    IsAllowed = await CanWithdrawAsync(userId),
                    Action = action,
                    Reason = hasActiveSubscription ? "Retrait en attente" : "Abonnement requis"
                },

                AmbassadorAction.ManagePlan => new AccessCheckResult
                {
                    IsAllowed = hasActiveSubscription,
                    Action = action,
                    Reason = hasActiveSubscription ? null : "Abonnement requis"
                },

                AmbassadorAction.ViewAdvancedStats => new AccessCheckResult
                {
                    IsAllowed = hasActiveSubscription,
                    Action = action,
                    Reason = hasActiveSubscription ? null : "Abonnement requis"
                },

                AmbassadorAction.ViewReferralNetwork => new AccessCheckResult
                {
                    IsAllowed = hasActiveSubscription,
                    Action = action,
                    Reason = hasActiveSubscription ? null : "Abonnement requis"
                },

                _ => new AccessCheckResult
                {
                    IsAllowed = false,
                    Action = action,
                    Reason = "Action inconnue"
                }
            };
        }
    }

    /// <summary>
    /// Enumération des actions qui nécessitent une vérification d'accès
    /// </summary>
    public enum AmbassadorAction
    {
        ViewWallet,
        ViewCommissions,
        Withdraw,
        ManagePlan,
        ViewAdvancedStats,
        ViewReferralNetwork
    }

    /// <summary>
    /// Résultat d'une vérification d'accès
    /// </summary>
    public class AccessCheckResult
    {
        public AmbassadorAction Action { get; set; }
        public bool IsAllowed { get; set; }
        public string Reason { get; set; }
    }
}
