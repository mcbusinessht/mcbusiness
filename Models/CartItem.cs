using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MCBusiness.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }
        [Required, ForeignKey("Cart")]
        public int CartId { get; set; }
        [Required, ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; }
        public virtual Cart Cart { get; set; }
        public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PriceSnapshot { get; set; }
        public ProductVariant ProductVariant { get; set; }
    }
}
