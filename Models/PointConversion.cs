using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class PointConversion
    {
        public int Id { get; set; } // Clé primaire
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        public ApplicationUser User { get; set; } // Navigation property vers ApplicationUser
        public int CurrencyBase { get; set; } // Clé étrangère vers Currency
        public Currency Currency { get; set; } // Navigation property vers Currency
        public int PointsUsed { get; set; } // Points utilisés pour la conversion
        public decimal Amount { get; set; } // Montant converti en monnaie
        public ConversionType ConversionType { get; set; } // Type de conversion
        public DateTimeOffset ConvertedAt { get; set; } = DateTimeOffset.UtcNow; // Date de la conversion
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique
    }
    public enum ConversionType
    {
        wallet_credit, // Conversion pour un crédit de portefeuille
        Cashout, // Conversion pour un achat
        Discount, // Conversion pour un retrait
        Gift, // Conversion pour un cadeau
        Other // Autre type de conversion
    }
}
