using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class PlatformTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public PlatformTransactionType Type { get; set; }

        [Required]
        public PlatformTransactionCategory Category { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        // NOUVEAU : ID de transaction externe pour idempotence
        [MaxLength(100)]
        public string? ExternalTransactionId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Référence optionnelle � une commande
        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        // Référence optionnelle à un abonnement
        public int? AmbassadorSubscriptionId { get; set; }
        public AmbassadorSubscription? AmbassadorSubscription { get; set; }

        // Référence optionnelle à un employé (ex: pour un salaire)
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Référence optionnelle à un vendeur (ex: pour SellerPayment)
        public string? SellerId { get; set; }
        public ApplicationUser? Seller { get; set; }

        // Référence au wallet MCBusiness (toujours cibl� ici)
        public int WalletId { get; set; }
        public Wallet Wallet { get; set; }
        public bool IsManual { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
    }

    public enum PlatformTransactionType
    {
        Revenue,        // Revenu reçu par MCBusiness
        Expense         // Dépense faite par MCBusiness
    }


    public enum PlatformTransactionCategory
    {
        // Catégories existantes (ordre conservé)
        Commission,             // Commission prélevée par MCBusiness (revenu)
        CommissionReferrer,     // Commission ambassadeur (dépense)
        DeliveryFee,            // Frais de livraison (revenu)
        SellerPayment,          // Paiement vers vendeur (dépense)
        EmployeePayment,        // Paiement salaire ou bonus employés (dépense)
        SubscriptionPayment,    // Paiement abonnement (revenu)
        OrderPayment,           // Paiement d'une commande (revenu)
        Refund,                 // Remboursement à client (dépense)
        SystemExpense,          // Dépense système (serveur, pub, etc.)
        ManualAdjustment,       // Ajustement manuel du solde
        OtherIncome,            // Autre revenu
        OtherExpense,           // Autre dépense
        
        // NOUVELLES CATÉGORIES OBLIGATOIRES
        UserDeposit,            // Dépôt utilisateur (revenu)
        UserWithdrawal,         // Retrait utilisateur (dépense)
        BankFees,               // Frais bancaires (dépense)
        ProcessingFees,         // Frais de traitement (MonCash, Stripe, etc.) (dépense)
        SubscriptionRefund,     // Remboursement abonnement (dépense)
        BonusPayment,           // Paiement bonus utilisateur (dépense)
        PromoDiscount,          // Coût promotion marketing (dépense)
        AmbassadorCommission    // Paiement commission ambassadeur (dépense) - NOUVEAU
    }


}
