using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Stock
    {
        [Key]
        public int Id { get; set; }
        [Required, ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; }
        public virtual ProductVariant ProductVariant { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTimeOffset? DateAdded { get; set; } = DateTimeOffset.UtcNow;
        public int? WarehouseId { get; set; }
    }

}
