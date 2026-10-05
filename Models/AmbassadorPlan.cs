using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    public class AmbassadorPlan : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } // Nom du plan
        [Required, Range(0, double.MaxValue)]
        public decimal Price { get; set; } // Prix du plan
        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public Currency Currency { get; set; }
        [Required, Range(1, 3650)]
        public int DurationInDays { get; set; } // Durée du plan en jours
        [MaxLength(1000)]
        public string? Description { get; set; } // Description
        [Required, ForeignKey("Category")]
        public int CategoryId { get; set; }
        // SIMPLIFIÉ: Un seul pourcentage de commission ambassadeur (plus de multi-génération pour commissions)
        [Range(0, 1)]
        public decimal AmbassadorCommissionPercentage { get; set; }
        [Range(0, double.MaxValue)]
        public decimal Ceiling { get; set; }
        public bool IsActive { get; set; } = true;
        [MaxLength(500)]
        public string? PathPicture { get; set; }
        public virtual Category Category {get; set;}

        #region NOUVEAU: RÈGLES DE POINTS DE FIDÉLITÉ

        /// <summary>
        /// Points attribués pour parrainage génération 1
        /// </summary>
        public int PointsGeneration1 { get; set; } = 0;
        
        /// <summary>
        /// Points attribués pour parrainage génération 2
        /// </summary>
        public int PointsGeneration2 { get; set; } = 0;
        
        /// <summary>
        /// Points attribués pour parrainage génération 3
        /// </summary>
        public int PointsGeneration3 { get; set; } = 0;
        
        /// <summary>
        /// Points bonus de bienvenue pour les nouveaux parrainés
        /// </summary>
        public int WelcomeBonusPoints { get; set; } = 0;
        
        /// <summary>
        /// Plafond mensuel de points de parrainage (0 = illimité)
        /// </summary>
        public int MonthlyPointsLimit { get; set; } = 0;
        
        /// <summary>
        /// Indique si c'est un plan gratuit (créé manuellement par admin)
        /// </summary>
        public bool IsFreeplan { get; set; } = false;

        #endregion


    }
}
