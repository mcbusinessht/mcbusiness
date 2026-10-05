using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class CharacteristicValue : BaseEntity
    {
        [Required, ForeignKey("Characteristic")]
        public int CharacteristicId { get; set; }
        [Required, MaxLength(100)]
        public string Value { get; set; }
        public Characteristic Characteristic { get; set; }
        public List<ProductVariantCharacteristic> ProductVariantCharacteristics { get; set; } = new();

    }
}
