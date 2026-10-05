using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class ShipmentMethod : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required, Range(0, double.MaxValue)]
        public decimal Cost { get; set; }
        [Required, Range(1, 365)]
        public int EstimatedDeliveryTime { get; set; } // En jours
        public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    }
}
