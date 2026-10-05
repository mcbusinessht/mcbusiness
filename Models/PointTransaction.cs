using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    /// <summary>
    /// Système universel et extensible de points de fidélité
    /// SÉPARÉ COMPLÈTEMENT du système d'argent (AmbassadorCommission)
    /// Responsable de TOUTES les attributions de points : parrainage, achats, challenges, campagnes, récompenses admin
    /// </summary>
    public class PointTransaction : BaseEntity
    {
        #region RÉFÉRENCES PRINCIPALES

        [Required, ForeignKey("User")]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        #endregion

        #region DÉTAILS TRANSACTION

        [Required]
        public int PointsAmount { get; set; } // Positif = gain, Négatif = consommation

        [Required, MaxLength(50)]
        public PointTransactionType Type { get; set; }

        [Required, MaxLength(200)]
        public string Description { get; set; }

        [MaxLength(500)]
        public string? Details { get; set; } // Informations supplémentaires JSON ou texte

        #endregion

        #region RÉFÉRENCES OPTIONNELLES

        // Référence vers l'événement source (optionnel)
        public int? ReferralId { get; set; } // Si lié à un parrainage
        public int? OrderId { get; set; } // Si lié à un achat
        public int? ChallengeId { get; set; } // Si lié à un challenge (futur)
        public int? CampaignId { get; set; } // Si lié à une campagne (futur)
        
        // ID de l'admin qui a attribué les points manuellement (si applicable)
        [ForeignKey("AdminUser")]
        public string? AdminUserId { get; set; }
        public ApplicationUser AdminUser { get; set; }

        #endregion

        #region MÉTADONNÉES

        [Required]
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? ExpiresAt { get; set; } // Expiration des points (optionnel)

        [Required]
        public PointTransactionStatus Status { get; set; } = PointTransactionStatus.Completed;

        // Solde des points APRÈS cette transaction (pour audit)
        public int? BalanceAfterTransaction { get; set; }

        #endregion

        #region NAVIGATION PROPERTIES

        [ForeignKey("ReferralId")]
        public Referral Referral { get; set; }

        [ForeignKey("OrderId")]
        public Order Order { get; set; }

        #endregion
    }

    /// <summary>
    /// Types de transactions de points
    /// </summary>
    public enum PointTransactionType
    {
        // Parrainage
        ReferralBonus,           // Points pour avoir parrainé quelqu'un
        WelcomeBonus,           // Points de bienvenue pour un nouvel utilisateur
        
        // Achats et activités
        PurchaseBonus,          // Points gagnés sur achats
        ReviewBonus,            // Points pour avoir écrit une review
        LoginBonus,             // Points de connexion quotidienne
        
        // Challenges et campagnes
        ChallengeCompletion,    // Points pour avoir complété un challenge
        CampaignReward,         // Points de campagne promotionnelle
        
        // Administration
        AdminGrant,             // Points attribués manuellement par admin
        AdminPenalty,           // Points retirés par admin (fraude, etc.)
        
        // Consommation
        RewardRedemption,       // Points consommés pour récompense
        GiftCardPurchase,       // Points échangés contre carte cadeau
        
        // Corrections
        Correction,             // Correction d'erreur
        Expiration              // Expiration automatique de points
    }

    /// <summary>
    /// Statuts des transactions de points
    /// </summary>
    public enum PointTransactionStatus
    {
        Pending,        // En attente de traitement
        Completed,      // Traitement terminé avec succès
        Cancelled,      // Annulée
        Expired,        // Expirée
        Reversed        // Annulée et inversée
    }
}
