using System;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Version pour la concurrence optimiste - évite les conflits de mise à jour
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// ID de l'utilisateur qui a créé ce compte (pour les admins)
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// ID de l'utilisateur qui a modifié ce compte en dernier
        /// </summary>
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Date de dernière modification du profil
        /// </summary>
        public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        public string? Gender { get; set; } // "Male", "Female", etc.
        public decimal Balance { get; set; } = 0; // Solde de l'utilisateur
        [Required]
        public string ReferralCode { get; set; } // Code de parrainage
        public string? ReferredBy { get; set; } // Code de parrainage du parrain
        public string? ProfilePictureUrl { get; set; } // URL de la photo de profil
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
        public DateTimeOffset RegistrationDate { get; set; } = DateTimeOffset.UtcNow;
        public PreferredLanguage PreferredLanguage { get; set; } = PreferredLanguage.fr;
        // Clé étrangère vers la table Currency
        public int? PreferredCurrencyId { get; set; } = 1;
        // Navigation property
        public Currency PreferredCurrency { get; set; }
        /// <summary>
        /// Suppression logique - évite la perte de données utilisateur
        /// </summary>
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique
        public bool IsVerified { get; set; } = false; // Pour gérer la vérification de l'utilisateur
        public bool IsAmbassador { get; set; } = false; // Pour gérer le statut d'ambassadeur
        public DateTimeOffset? LastLoginDate { get; set; } // Date de la dernière connexion

        /// <summary>
        /// Messagerie: dernière activité sur le hub (UTC)
        /// </summary>
        public DateTimeOffset? LastSeenAtUtc { get; set; }

        public string? UrlPictureIdentityDocument { get; set; } // URL de la photo de la pièce d'identité
        public string? UrlPictureIdentityDocument2 { get; set; } // URL de la photo de la pièce d'identité 2
        public virtual ICollection<Wallet> Wallets { get; set; }

        /// <summary>
        /// NOUVEAU: Points de fidélité totaux (toujours mis à jour via PointTransaction)
        /// </summary>
        public int TotalPoints { get; set; } = 0;

    }

    public enum PreferredLanguage{
        fr,
        ht,
        en,
        es,
        pt
    }
}
