using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    /// <summary>
    /// Commission dédiée au système Ambassadeur multi-entreprises
    /// Séparée de la Commission existante pour éviter les conflits
    /// </summary>
    public class AmbassadorCommission : BaseEntity
    {
        #region RÉFÉRENCES PRINCIPALES
        
        [Required, ForeignKey("Ambassador")]
        public string AmbassadorId { get; set; }
        public ApplicationUser Ambassador { get; set; }
        
        [Required, ForeignKey("Order")]
        public int OrderId { get; set; }
        public Order Order { get; set; }
        
        [Required, ForeignKey("Company")]
        public int CompanyId { get; set; }
        public Company Company { get; set; }
        
        #endregion

        #region DÉTAILS COMMISSION
        
        [Required, Range(0, double.MaxValue)]
        public decimal Amount { get; set; }
        
        [Required, Range(0, 1)]
        public decimal CommissionRate { get; set; } // Taux appliqué (ex: 0.05 = 5%)
        
        [Required]
        public AmbassadorCommissionType Type { get; set; }
        
        // SUPPRIMÉ: Generation - une commission = un montant final par commande/entreprise/ambassadeur
        
        #endregion

        #region GESTION STATUTS ET TIMING
        
        [Required]
        public AmbassadorCommissionStatus Status { get; set; } = AmbassadorCommissionStatus.Pending;
        
        public DateTime? ApprovedAt { get; set; }    // Date d'approbation
        public DateTime? AvailableAt { get; set; }   // Date de disponibilité (après délai)
        public DateTime? PaidAt { get; set; }        // Date de paiement effectif
        
        public string? RejectionReason { get; set; } // Raison du rejet si applicable
        
        #endregion

        #region MÉTADONNÉES BUSINESS
        
        // Devise liée à la table Currency (C)
        [Required, ForeignKey("Currency")]
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }

        // Champs de snapshot léger (B)
        public string? PlanNameSnapshot { get; set; }
        // SUPPRIMÉ: ProductPriceSnapshot - le prix est déjà figé dans OrderProduct.PriceSnapshot

        public string? ProcessingNotes { get; set; } // Notes de traitement
        public bool IsRecurring { get; set; } = false; // Pour les commissions récurrentes
        
        // Référence vers la transaction wallet associée
        [ForeignKey("WalletTransaction")]
        public int? WalletTransactionId { get; set; }
        public WalletTransaction WalletTransaction { get; set; }
        
        #endregion

        #region COMPUTED PROPERTIES
        
        /// <summary>
        /// Vérifie si la commission peut être payée
        /// </summary>
        public bool CanBePaid => Status == AmbassadorCommissionStatus.Available 
                                && AvailableAt.HasValue 
                                && AvailableAt.Value <= DateTime.UtcNow;
        
        /// <summary>
        /// Vérifie si la commission est encore dans la période de grâce
        /// </summary>
        public bool IsInGracePeriod => Status == AmbassadorCommissionStatus.Pending
                                     && AvailableAt.HasValue 
                                     && AvailableAt.Value > DateTime.UtcNow;
        
        #endregion
    }

    /// <summary>
    /// Type de commission ambassadeur
    /// </summary>
    public enum AmbassadorCommissionType
    {
        DirectSale,          // Commission directe sur vente
        ReferralBonus,       // Bonus de parrainage
        MonthlyBonus,        // Bonus mensuel (si applicable)
        PerformanceBonus     // Bonus de performance
    }

    /// <summary>
    /// Statuts avancés pour les commissions ambassadeur
    /// </summary>
    public enum AmbassadorCommissionStatus
    {
        Pending,      // En attente de validation
        Approved,     // Approuvée mais pas encore disponible
        Available,    // Disponible pour paiement
        Paid,         // Payée et créditée
        Rejected,     // Rejetée
        Cancelled,    // Annulée (ex: commande annulée)
        Refunded      // Remboursée (ex: retour produit)
    }
}
