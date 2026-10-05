using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MCBusiness.Models
{
    public class Shipment : BaseEntity
    {
        [Required, ForeignKey("OrderProduct")]
        public int OrderProductId { get; set; }
        [Required, MaxLength(200)]
        public string TrackingNumber { get; set; }
        [Required, ForeignKey("DeliveryPerson")]
        public int DeliveryPersonId { get; set; }
        public DeliveryPerson DeliveryPerson { get; set; }
        [Required]
        public DateTimeOffset ShippingDate { get; set; }
        public DateTimeOffset? DeliveryDate { get; set; }

        [Required]
        public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;
        public virtual OrderProduct OrderProduct { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }

    }

    public enum ShipmentStatus
    {
        Pending,
        InTransit,
        Delivered,
        Canceled
    }
}
