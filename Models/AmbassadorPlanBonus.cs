using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class AmbassadorPlanBonus
    {
        public int Id { get; set; } // Clé primaire
        public string Name { get; set; } // Nom du bonus
        public string Description { get; set; } // Description du bonus
        public decimal BonusAmount { get; set; } // Montant du bonus
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Date de création du bonus
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique
        
    }
}
