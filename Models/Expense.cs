using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        // Montant de la dépense
        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal Amount { get; set; }

        // Description (optionnelle) de la dépense
        public string? Description { get; set; }

        // Date à laquelle la dépense a été faite
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Catégorie de la dépense (ex: Refund, Commission, Salary, Hosting, etc.)
        public ExpenseCategory Category { get; set; }

        // Moyen de paiement utilisé pour cette dépense (Wallet, Bank, Cash, etc.)
        public PaymentMethod PaymentMethod { get; set; }

        // Id de l'utilisateur ou entité ayant reçu la dépense (si applicable)
        public string? ReceiverUserId { get; set; }
        public ApplicationUser? ReceiverUser { get; set; }

        // Référence de la transaction plateforme liée
        public int? PlatformTransactionId { get; set; }
        public PlatformTransaction? PlatformTransaction { get; set; }

        // Qui a validé/payé cette dépense
        public string? ApprovedByUserId { get; set; }
        public ApplicationUser? ApprovedByUser { get; set; }
        // Indique si la dépense a été supprimée
        public bool IsDeleted { get; set; } = false;
    }

    public enum ExpenseCategory
    {
        Refund ,
        Commission ,
        SellerPayment ,
        Salary ,
        HostingFee ,
        Marketing ,
        Infrastructure ,
        Transport ,
        Tax ,
        Other 
    }


}
