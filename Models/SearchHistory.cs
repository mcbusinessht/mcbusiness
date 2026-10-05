using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    public class SearchHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Query { get; set; } // Texte de la recherche

        public string? FilterUsed { get; set; } // Optionnel : filtres utilisés

        public string? SearchLocation { get; set; } // Optionnel : zone de recherche (ex : "Produits", "Commandes", etc.)

        public string? UserId { get; set; } // Utilisateur (null si non connecté)
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public DateTimeOffset SearchedAt { get; set; } = DateTimeOffset.UtcNow; // Date/heure de la recherche

        public string? IpAddress { get; set; } // Optionnel : IP de l�utilisateur
    }
}
