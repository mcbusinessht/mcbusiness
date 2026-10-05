using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;         // ? Manquant
using System.ComponentModel.DataAnnotations.Schema; // ? Manquant
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class PromoCode : BaseEntity
    {
        [Required, MaxLength(20)]
        public string Code { get; set; }

        // ?? Lien vers la campagne
        [Required, ForeignKey("PromoCampaign")]
        public int PromoCampaignId { get; set; }
        public virtual PromoCampaign PromoCampaign { get; set; }

        [Required, ForeignKey("CreatedByUser")]
        public string CreatedByUserId { get; set; }
        public virtual ApplicationUser CreatedByUser { get; set; }

        [Column(TypeName = "decimal(5,2)"), Range(0, 100)]
        public decimal DiscountPercentage { get; set; }

        [Column(TypeName = "decimal(10,2)"), Range(0, double.MaxValue)]
        public decimal RewardAmount { get; set; }

        // NOUVEAU : Qui supporte le coût de la promo (règle financière critique)
        [Required]
        public PromoImpactTarget ImpactTarget { get; set; } = PromoImpactTarget.PlatformOnly;

        public DateTimeOffset? ExpiresAt { get; set; }

        public bool IsOrderAvailable { get; set; } = false;
        public bool IsPlanAvailable { get; set; } = false;
        public bool IsITServiceAvailable { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int UsageLimit { get; set; } = 1;
        [Range(0, int.MaxValue)]
        public int UsageCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public virtual List<PromoCodeUsage> Usages { get; set; } = new();

        [NotMapped]
        public bool IsValid => IsActive && (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow) && UsageCount < UsageLimit;
    }

    /// <summary>
    /// Définit qui supporte financièrement le coût d'une promotion
    /// RÈGLE : Par défaut, c'est toujours la plateforme qui paye
    /// </summary>
    public enum PromoImpactTarget
    {
        /// <summary>
        /// La plateforme supporte 100% du coût de la promo (défaut)
        /// Le vendeur reçoit son montant normal, la plateforme perd la différence
        /// </summary>
        PlatformOnly = 0,

        /// <summary>
        /// Le vendeur supporte 100% du coût de la promo
        /// La plateforme prélève sa commission sur le prix réduit
        /// RARE : seulement si accord explicite avec le vendeur
        /// </summary>
        SellerOnly = 1,

        /// <summary>
        /// Coût partagé entre plateforme et vendeur selon un ratio défini
        /// TRÈS RARE : seulement pour campagnes spéciales co-financées
        /// </summary>
        Shared = 2
    }
}
