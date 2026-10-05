using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class UserPoint
    {
        public int Id { get; set; } // Clé primaire
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser    
        public ApplicationUser User { get; set; } // Navigation property vers ApplicationUser
        public int TotalPoints { get; set; } // Points accumul�s
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow; // Date de la dernière mise à jour des points


    }
}
