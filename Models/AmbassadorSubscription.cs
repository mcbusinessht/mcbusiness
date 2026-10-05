using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class AmbassadorSubscription : BaseEntity
    {
        [Required, ForeignKey("User")]
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        [Required, ForeignKey("Plan")]
        public int PlanId { get; set; } // Clé étrangère vers Plan
        [Required]
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow; // Date de d�but
        [Required]
        public DateTimeOffset EndDate { get; set; } // Calcul�e automatiquement
        [Required]
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active; // Statut de l'abonnement
        public virtual ApplicationUser User { get; set; }
        public virtual AmbassadorPlan Plan { get; set; }
        [Range(0, int.MaxValue)]
        public int ReferralCount { get; set; } = 0; // Compteur de parrainage
        public virtual Payment Payment { get; set; }


        public void CalculateEndDate(int durationInDays)
        {
            EndDate = StartDate.AddDays(durationInDays);
        }
    }

    public enum SubscriptionStatus
    {
        Active,
        Inactive,
        Cancelled
    }
}
