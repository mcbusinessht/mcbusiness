using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class ReturnRequest
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OrderProduct")]
        public int OrderProductId { get; set; }
        public virtual OrderProduct OrderProduct { get; set; }

        [Required]
        public DateTimeOffset RequestDate { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; }  // "Defective", "WrongItem", etc.

        [MaxLength(500)]
        public string Details { get; set; }

        [Required]
        public ReturnStatus Status { get; set; } = ReturnStatus.Pending;

        public DateTimeOffset? ApprovalDate { get; set; }

        public DateTimeOffset? CompletionDate { get; set; }

        [MaxLength(500)]
        public string AdminComments { get; set; }

        // Pour suivre le retour physique
        public string ReturnTrackingNumber { get; set; }
        public DateTimeOffset? ReturnShippedDate { get; set; }
        public DateTimeOffset? ReturnReceivedDate { get; set; }

        // Pour le remboursement
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? RefundAmount { get; set; }
        public DateTimeOffset? RefundDate { get; set; }
        public string RefundTransactionId { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    public enum ReturnStatus
    {
        Pending,        // Demande initiale
        Approved,       // Approuvé par l'admin
        Rejected,       // Rejeté par l'admin
        ReturnShipped,  // Client a renvoyé le produit
        ReturnReceived, // Produit reçu par le vendeur
        Refunded,       // Remboursement effectué
        Completed       // Processus terminé
    }
}
