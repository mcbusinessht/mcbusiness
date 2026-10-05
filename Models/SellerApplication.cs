using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class SellerApplication : BaseEntity
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; }

        [Required, MaxLength(100), EmailAddress]
        public string Email { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Required, MaxLength(200)]
        public string BusinessName { get; set; }

        [Required, MaxLength(100)]
        public string BusinessType { get; set; } // Ex: "Retail", "Wholesale", "Manufacturing", etc.

        [MaxLength(1000)]
        public string Description { get; set; }

        [Required, MaxLength(200)]
        public string Address { get; set; }

        [Required, MaxLength(100)]
        public string City { get; set; }

        [Required, MaxLength(100)]
        public string Country { get; set; }

        [MaxLength(500)]
        public string IdentityDocumentUrl { get; set; } // URL du document d'identité uploadé

        [Required]
        public SellerApplicationStatus Status { get; set; } = SellerApplicationStatus.Pending;

        public DateTimeOffset? ReviewedAt { get; set; }

        [MaxLength(450)]
        public string ReviewedBy { get; set; } // ID de l'admin qui a reviewé

        [MaxLength(1000)]
        public string ReviewNotes { get; set; } // Notes de l'admin lors de la review
    }

    public enum SellerApplicationStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
