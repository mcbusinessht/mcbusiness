using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Transaction : BaseEntity
    { // Clé primaire
        [Required , ForeignKey("User")]
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0")]
        public decimal Amount { get; set; } // Montant de la transaction
        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public Currency Currency { get; set; }
        [Required]
        public TransactionType Type { get; set; } // Type de la transaction
        public WalletType WalletType { get; set; } = WalletType.Customer;
        public int? WalletId { get; set; }
        [Required]
        public TransactionMethod Method { get; set; } // Methode Utiliser la transaction
        [Required, MaxLength(500)]
        public string Description { get; set; } // Détails de la transaction

        [Required]
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending; // Statut de la transaction
        [MaxLength(200)]
        public string? TransactionId { get; set; }
        [MaxLength(100)]
        public string? OrderId { get; set; }
        [Required]
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow; // Date de la transaction
        public DateTimeOffset? CompletedAt { get; set; } // Date de completion
        public virtual ApplicationUser User { get; set; }
        // IsDeleted héritée de BaseEntity - UpdatedAt géré par ModifiedAt

        
    }

    public enum TransactionType
    {
        Deposit,
        Withdrawal,
        Bonus,
        Payment,
        Transfer,
        None,
        Refund
    }

    public enum TransactionMethod
    {
        MonCash,
        PayPal,
        Natcash,
        BankTransfer,
        Zelle,
        Card,
        CreditCard,
        Stripe,
        Cash,
        MCBusinessPay
    }


    public enum TransactionStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
        
    }


}
