using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Order : BaseEntity
    {

        [Required]
        public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;

        [Required, ForeignKey("User")]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        [Required, Column(TypeName = "decimal(18, 2)"), Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public virtual Currency Currency { get; set; }

        [Required, ForeignKey("Address")]
        public int AddressId { get; set; }
        public Address Address { get; set; }

        
        [Required, MaxLength(50)]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public ICollection<OrderProduct> Products { get; set; } = new List<OrderProduct>();
        public Payment Payment { get; set; }
        public Shipment Shipment { get; set; }

        #region AMBASSADOR SYSTEM
        
        /// <summary>
        /// Code de référence de l'ambassadeur ayant généré cette commande
        /// </summary>
        public string? ReferredByAmbassador { get; set; }
        
        /// <summary>
        /// Navigation vers l'ambassadeur réf�rent
        /// </summary>
        [ForeignKey("ReferredByAmbassador")]
        public ApplicationUser? ReferringAmbassador { get; set; }
        
        /// <summary>
        /// Commissions générées par cette commande
        /// </summary>
        public virtual ICollection<AmbassadorCommission> AmbassadorCommissions { get; set; } = new List<AmbassadorCommission>();
        
        #endregion

        [Column(TypeName = "decimal(18, 2)"), Range(0, double.MaxValue)]
        public decimal ShippingTotal { get; set; }
        // UpdatedAt géré par BaseEntity (ModifiedAt)
        // IsDeleted héritée de BaseEntity
        public bool IsReceived { get; set; } = false;

        [NotMapped]
        public decimal GrandTotal => TotalAmount  + ShippingTotal;
    }
    public enum OrderStatus{
        Pending,
        Paid,
        PartiallyShipped,
        Shipped,
        PartiallyDelivered,
        Delivered,
        PartiallyReturned,
        Cancelled,
        Refunded

    }
}
