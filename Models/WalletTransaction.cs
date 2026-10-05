using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace MCBusiness.Models
{
    public class WalletTransaction
    {
        [Key]
        public int Id { get; set; } // Clé primaire
        [ForeignKey("Wallet")]
        public int WalletId { get; set; } // Clé étrangère vers Wallet
        public virtual Wallet Wallet { get; set; }
        [ForeignKey("User")]
        public string UserId { get; set; } // Clé étrangère vers ApplicationUser
        public decimal Amount { get; set; } // Montant de la transaction
        public WalletTransactionType Type { get; set; } // Type de transaction (remboursement, commission, vente)
        public WalletType WalletType { get; set; } = WalletType.Customer;
        public decimal Balance { get; set; }=0m; // Solde du portefeuille
        public string? TransactionId { get; set; } // Référence vers Transaction (ID externe)
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Date de création
        public string? Description { get; set; } // Description
        [ForeignKey("Currency")]
        public int CurrencyBase { get; set; } =1;
        public Currency Currency {get; set; } 
        public bool IsDeleted { get; set; } = false; // Pour gérer la suppression logique
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow; // Date de la dernière mise à jour
        

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
    public enum WalletTransactionType
    {
        Refund,
        Commission,
        Sales,
        Bonus,
        Deposit,
        Withdrawal,
        Transfer,
        Payment,
        
        // AMBASSADOR SYSTEM - Types de commission spécifiques
        CommissionSale,      // Commission directe sur vente
        CommissionReferral,  // Commission bonus parrainage
        CommissionLocked     // Commission verrouill�e (en attente)
    }
}
