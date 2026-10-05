using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class ProductVariant : BaseEntity
    {
        
        [Required, ForeignKey("Product")]
        public int ProductId { get; set; }
        [MaxLength(50)]
        public string? Sku { get; set; }
        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Le prix doit être supérieur à 0")]
        public decimal Price { get; set; }
        public decimal? PriceSnapshot { get; set; }
        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public Currency Currency { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "La quantité ne peut pas être négative")]
        public int Quantity { get; set; }
        public bool IsAvailable { get; set; } = true;
        public ShippingPackageType ShippingPackageType { get; set; } = ShippingPackageType.Small;
        [Range(0, double.MaxValue, ErrorMessage = "Le poids ne peut pas etre negatif")]
        public decimal? WeightKg { get; set; }
        public bool RequiresSpecialHandling { get; set; } = false;
        public bool ShippingQuoteRequired { get; set; } = false;
        // IsDeleted héritée de BaseEntity
        public virtual Product Product { get; set; }
        public List<ProductVariantCharacteristic> ProductVariantCharacteristics { get; set; } = new();

        public List<ProductImage> Images { get; set; } =new();
        public Stock Stock { get; set; }
        
        /// <summary>
        /// Paliers de prix en fonction de la quantité
        /// </summary>
        public List<PriceBreak> PriceBreaks { get; set; } = new();

    }

    public enum ShippingPackageType
    {
        Any,
        Small,
        Medium,
        Large,
        Heavy,
        VeryHeavy,
        QuoteRequired
    }
}

