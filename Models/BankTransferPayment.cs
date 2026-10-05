using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{

    public class BankTransferPayment
    {
        public int Id { get; set; }

        // Utilisateur ayant soumis le transfert
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int TransactionId { get; set; }
        public Transaction Transaction { get; set; }
        public int? WalletId { get; set; }
        public WalletType WalletType { get; set; } = WalletType.Customer;

        // Informations bancaires
        [Required]
        [MaxLength(100)]
        public string BankName { get; set; }

        [Required]
        [MaxLength(100)]
        public string AccountName { get; set; }

        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; set; }


        [Required]
        public BankTransferMethod TransferMethod { get; set; }  // Local/SPIH/SWIFT

        [Required]
        public BankTransferDirection Direction { get; set; } = BankTransferDirection.Deposit;    // Deposit/Withdrawal

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Amount { get; set; }

        // Devise du paiement
        [Required]
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }

        // Fichier de preuve (chemin ou URL)
        [MaxLength(255)]
        public string ProofFilePath { get; set; }

        // état de validation
        public BankTransferStatus Status { get; set; } = BankTransferStatus.Pending;

        public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

        // Revue administrative
        public DateTimeOffset? ReviewedAt { get; set; }

        public string ReviewedBy { get; set; }

        [MaxLength(500)]
        public string AdminComment { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    
    public enum BankTransferMethod
    {
        Local,  // Virement local
        SPIH,   // Syst�me de Paiement Interbancaire Ha�tien
        SWIFT   // Virement international
    }

    public enum BankTransferDirection 
    {
        Deposit,    // Dépôt (entrant)
        Withdrawal  // Retrait (sortant)
    }
    public enum BankTransferStatus
    {
        Pending,
        Approved,
        Rejected
    }


}
