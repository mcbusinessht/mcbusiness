using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class AmbassadorPlanBonusMapping
    {
        public int Id { get; set; } // Clé primaire
        public int AmbassadorPlanId { get; set; } // Clé étrangère vers AmbassadorPlan
        public virtual AmbassadorPlan AmbassadorPlan { get; set; } // Navigation property vers AmbassadorPlan
        public int AmbassadorPlanBonusId { get; set; } // Clé étrangère vers AmbassadorPlanBonus
        public virtual AmbassadorPlanBonus PlanBonus { get; set; } // Navigation property vers AmbassadorPlanBonus
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique

    }
}
