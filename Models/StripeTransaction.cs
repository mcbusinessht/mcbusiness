using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class StripeTransaction
    {
        [Key]
        public int Id { get; set; }

        // FK vers la table Transaction globale
        [Required, ForeignKey("Transaction")]
        public int TransactionId { get; set; }
        public Transaction Transaction { get; set; }

        // Identifiant Stripe du PaymentIntent
        [Required, MaxLength(100)]
        public string PaymentIntentId { get; set; }

        // Montant (en centimes) et devise
        [Required]
        public long Amount { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; }

        // Statut Stripe : requires_payment_method, requires_action, succeeded�
        [Required, MaxLength(50)]
        public string Status { get; set; }

        // Date de création côté Stripe
        public DateTimeOffset CreatedAt { get; set; }

        // Date de confirmation ou d�échec
        public DateTimeOffset? CompletedAt { get; set; }

        // Stocke temporairement le client secret (TTL très court, ou null après usage)
        [MaxLength(255)]
        public string ClientSecret { get; set; }
    }
}
