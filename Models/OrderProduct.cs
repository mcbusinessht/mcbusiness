using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class OrderProduct : BaseEntity
    {
        [Required, ForeignKey("Order")]
        public int OrderId { get; set; }
        [Required, ForeignKey("Company")]
        public int CompanyId { get; set; }
        public virtual Company Company { get; set; }
        [Required, ForeignKey("ProductVariant")]
        public int ProductVariantId { get; set; }
        [Required, Range(1, int.MaxValue, ErrorMessage = "La quantité doit être d'au moins 1")]
        public int Quantity { get; set; }
        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Le prix doit être supérieur à 0")]
        public decimal PriceAtOrder { get; set; }
        [Required]
        public OrderProductStatus Status { get; set; } = OrderProductStatus.Pending;
        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public virtual Currency Currency { get; set; }
        [Range(0, double.MaxValue)]
        public decimal DeliveryFee { get; set; } = 0;
        public bool IsReceived { get; set; } = false;
        public bool IsRefunded { get; set; } = false;


        [NotMapped] 
        public decimal SubTotal => PriceAtOrder * Quantity;
        public virtual Order Order { get; set; }
        public virtual ProductVariant ProductVariant { get; set; }
        public Shipment? Shipment { get; set; }
        
    }

    public enum OrderProductStatus{
        Pending,
        Shipped,
        Delivered,
        Cancelled,
        ReturnRequested,
        Returned

    }
}
