using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class PromoCodeUsage : BaseEntity
    {
        [Required, ForeignKey("PromoCode")]
        public int PromoCodeId { get; set; }
        public virtual PromoCode PromoCode { get; set; }

        [Required, ForeignKey("UsedByUser")]
        public string UsedByUserId { get; set; } // le filleul
        public virtual ApplicationUser UsedByUser { get; set; }
        
        [Required]
        public DateTimeOffset UsedAt { get; set; } = DateTimeOffset.UtcNow;

        // ? Recommandé : montant réellement appliqué
        [Column(TypeName = "decimal(10,2)"), Range(0, double.MaxValue)]
        public decimal DiscountApplied { get; set; } = 0;
        
        // Alias pour compatibilité avec le code existant
        public decimal DiscountAmount 
        { 
            get => DiscountApplied; 
            set => DiscountApplied = value; 
        }

        // ? Recommandé : montant récompensé au parrain
        [Column(TypeName = "decimal(10,2)"), Range(0, double.MaxValue)]
        public decimal RewardGiven { get; set; } = 0;

        public virtual Payment? Payment { get; set; } // Lien vers le paiement associé
    }
}
