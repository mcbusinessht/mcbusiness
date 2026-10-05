using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class DeliveryPerson : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required, MaxLength(20)]
        public string ContactNumber { get; set; }
        public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    }
}
