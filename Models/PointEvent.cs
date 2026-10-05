using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class PointEvent
    {
        public int Id { get; set; } // Clé primaire
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        public ApplicationUser User { get; set; } // Navigation property vers ApplicationUser
        public EventType EventType { get; set; } // Type d'événement
        public int PointsEarned { get; set; } // Points gagnés
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Date de création de l'événement
        public string Description { get; set; } // Description de l'événement
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique
    }
    public enum EventType
    {
        monthly_sales,
        Referral, // Parrainage
        Purchase, // Achat
        Bonus, // Bonus
        Other,// Autre type d'événement
        Inscription_Bonus
        

    }
}
