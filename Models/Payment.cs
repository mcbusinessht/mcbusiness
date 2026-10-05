using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    public class Payment : BaseEntity
    {
        [ForeignKey("User")]
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }
        
        [ForeignKey("Order")]
        public int? OrderId { get; set; }
        public virtual Order? Order { get; set; }
        public int? AmbassadorSubscriptionId { get; set; }
        public virtual AmbassadorSubscription? AmbassadorSubscription { get; set; }

        [Required, MaxLength(50)]
        public PaymentMethod PaymentMethod { get; set; } // Carte, PayPal, Crypto
        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0")]
        public decimal Amount { get; set; }
        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public Currency Currency { get; set; }

        public int? PromoCodeUsageId { get; set; } // le code promo utilisé
        public virtual PromoCodeUsage PromoCodeUsage { get; set; }

        [MaxLength(200)]
        public string? GatewayTransactionId { get; set; }
        [MaxLength(200)]
        public string? TransactionId { get; set; }
        [Required]
        public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;
        [Required]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        // IsDeleted héritée de BaseEntity
        public string? Note { get; set; } // pour ajouter des notes sur le paiement


    }

    public enum PaymentMethod
    {
        MonCash,
        NatCash,
        PayPal,
        Zelle,
        Card,
        BankTransfer,
        MCBusinessPay,
        Cash,
        Wallet,
        Other
        
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded
        
    }
}
