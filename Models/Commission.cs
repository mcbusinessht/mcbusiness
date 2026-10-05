using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class Commission : BaseEntity
    {
        [Required, ForeignKey("User")]
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        public ApplicationUser User { get; set; } // Navigation property vers ApplicationUser
        [Required, ForeignKey("Order")]
        public int OrderId { get; set; } // Clé étrangère vers Order
        public Order Order { get; set; } // Navigation property vers Order
        [Required, Range(0, double.MaxValue)]
        public decimal Amount { get; set; } // Montant de la commission
        [Required]
        public CommissionStatus Status { get; set; } = CommissionStatus.Pending; // Statut de la commission
        public bool IsValid { get; set; } = false; // Pour gérer la validation de la commission
    }

    public enum CommissionStatus
    {
        Pending, // En attente
        Approved, // Approuvée
        Rejected // Rejetée
    }
}
