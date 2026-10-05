using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    /// <summary>
    /// Définit les paliers de prix en fonction de la quantité (comme sur Alibaba)
    /// Exemple: 1-9 pcs = 30$, 10-49 pcs = 25$, 50+ pcs = 20$
    /// </summary>
    public class PriceBreak : BaseEntity
    {
        [Required, ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        
        /// <summary>
        /// Quantité minimum pour ce palier de prix (incluse)
        /// </summary>
        [Required, Range(1, int.MaxValue, ErrorMessage = "La quantité minimum doit être supérieure à 0")]
        public int MinQuantity { get; set; }
        
        /// <summary>
        /// Quantité maximum pour ce palier de prix (incluse, null = illimité)
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "La quantité maximum doit être supérieure à 0")]
        public int? MaxQuantity { get; set; }
        
        /// <summary>
        /// Prix unitaire pour ce palier de quantité
        /// </summary>
        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Le prix doit être supérieur à 0")]
        public decimal UnitPrice { get; set; }
        
        /// <summary>
        /// Pourcentage de réduction par rapport au prix de base (optionnel, pour affichage)
        /// </summary>
        [Range(0, 100, ErrorMessage = "La réduction doit être entre 0 et 100%")]
        public decimal? DiscountPercentage { get; set; }
        
        /// <summary>
        /// Label d'affichage pour ce palier (ex: "Prix en gros", "Prix détail")
        /// </summary>
        [MaxLength(50)]
        public string? DisplayLabel { get; set; }
        
        /// <summary>
        /// Ordre d'affichage des paliers
        /// </summary>
        public int SortOrder { get; set; }
        
        /// <summary>
        /// Si ce palier est actif ou non
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual ProductVariant ProductVariant { get; set; }
    }
}
