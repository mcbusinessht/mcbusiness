using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class ProductVariantCharacteristic : BaseEntity
    {
        [Required, ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        [Required, ForeignKey("Characteristic")]
        public int CharacteristicId { get; set; }
        public Characteristic Characteristic { get; set; }

        [Required, ForeignKey("CharacteristicValue")]
        public int CharacteristicValueId { get; set; }
        public CharacteristicValue CharacteristicValue { get; set; }
    }

}
