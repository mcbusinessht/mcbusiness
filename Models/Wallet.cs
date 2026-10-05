using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Wallet
    {
        [Key]
        public int Id { get; set; } // Clé primaire
        [ForeignKey("User")]
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        public WalletType Type { get; set; } = WalletType.Customer;
        public decimal Balance { get; set; }=0; // Solde du portefeuille
        [ForeignKey("Currency")]
        public int CurrencyBase { get; set; } =1;
        public Currency Currency {get; set; } 
        public DateTimeOffset? CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Date de création
        public DateTimeOffset? LastUpdated { get; set; } = DateTimeOffset.UtcNow; // Dernière mise à jour
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow; // Date de la dernière mise à jour
        // public bool IsDefault { get; set; } = false; // Portefeuille par défaut
        // public decimal? BalanceLimit { get; set; } = null; // null = pas de limite
        

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
    
    public enum WalletType
    {
        Customer,
        Earnings,
        Platform
    }
}
