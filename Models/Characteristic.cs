using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Characteristic : BaseEntity
    {
        [Required, MaxLength(50)]
        public string Name { get; set; }
        [Required, ForeignKey("Category")]
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; }
        public ICollection<CharacteristicValue> Values { get; set; }
        public List<ProductVariantCharacteristic> ProductVariantCharacteristics { get; set; } = new();

    
    }
}
