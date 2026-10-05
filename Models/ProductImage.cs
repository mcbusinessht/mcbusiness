using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class ProductImage : BaseEntity
    {
        [Required, ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        [Required, MaxLength(500)]
        public string Url { get; set; }
        public ProductVariant ProductVariant { get; set; }  
    }
}
