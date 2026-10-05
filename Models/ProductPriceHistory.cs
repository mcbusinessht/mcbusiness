using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class ProductPriceHistory
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        public decimal Price { get; set; }
        public DateTimeOffset EffectiveDate { get; set; }
        

        public virtual ProductVariant ProductVariant { get; set; }
    }

}
