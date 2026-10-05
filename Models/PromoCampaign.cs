using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable


namespace MCBusiness.Models
{
    public class PromoCampaign : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } // Ex : "Lancement Avril", "Promo été"

        [Required]
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? EndDate { get; set; }

        public bool IsActive { get; set; } = true;
        [MaxLength(500)]
        public string? Description { get; set; } // Ex : "Lancement de la nouvelle gamme de produits", "Promo spéciale pour les étudiants"
        public virtual List<PromoCode> PromoCodes { get; set; } = new();
    }

}
