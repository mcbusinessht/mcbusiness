using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    

    public class SellerPayment : BaseEntity
    {
        // Vendeur concerné (Company = vendeur)
        [Required]
        public int CompanyId { get; set; }
        public virtual Company Company { get; set; }

        [Required]
        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Required]
        public int CurrencyBase { get; set; } = 1;
        public virtual Currency Currency { get; set; }

        // Montant des produits (prix original affiché)
        [Required, Column(TypeName = "decimal(18, 2)"), Range(0, double.MaxValue)]
        public decimal ProductTotal { get; set; }

        // Frais de livraison
        [Column(TypeName = "decimal(18, 2)"), Range(0, double.MaxValue)]
        public decimal DeliveryFee { get; set; }
        [Column(TypeName = "decimal(18, 2)"), Range(0, 1)]
        public decimal SalesCommission { get; set; } = 0.10m;
        
        // Commission de la plateforme (BASE SEULEMENT)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? PlatformCommission { get; set; }
        
        // Montant versé au vendeur (après commission)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? SellerAmount { get; set; }

        // ============== NOUVEAUX CHAMPS POUR FORMULE FINANCIÈRE OFFICIELLE ==============
        
        // Montant brut total (ProductTotal + DeliveryFee)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? GrossAmount { get; set; }
        
        // Commission totale plateforme (Commission base + Ambassadeur + Promo)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? PlatformCommissionTotal { get; set; }

        // ============== NOUVEAUX CHAMPS AMAZON-LIKE (ajoutés progressivement) ==============
        
        // Montant réellement payé par le client (après promo)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ActualPaidAmount { get; set; }
        
        // Montant de la remise promo (si applicable)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PromoDiscountAmount { get; set; } = 0;
        
        // Commission ambassadeur déduite (si applicable)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmbassadorCommissionAmount { get; set; } = 0;
        
        // ID de l'ambassadeur (pour traçabilité)
        [MaxLength(450)]
        public string? AmbassadorId { get; set; }
        
        // Taux de commission plateforme appliqué (snapshot)
        [Column(TypeName = "decimal(5, 4)")]
        public decimal PlatformCommissionRate { get; set; } = 0.10m; // 10% par défaut
        
        // JSON snapshot complet du calcul (audit trail)
        [Column(TypeName = "text")]
        public string? CalculationSnapshot { get; set; }

        // Total général
        [NotMapped]
        public decimal GrandTotal => ProductTotal + DeliveryFee;

        [NotMapped]
        public decimal SubTotal => GrandTotal - (ProductTotal * SalesCommission);


        // Date de création ou paiement
        [Required]
        public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;

        // ? Si le paiement a bien été versé au vendeur
        public bool IsPaidToSeller { get; set; } = false;

        // ? Date de paiement réel au vendeur
        public DateTimeOffset? PaidToSellerAt { get; set; }

        // ? Date de suppression (soft delete)
        public DateTimeOffset? DeletedAt { get; set; }

        // ? Statut général du paiement (en attente, réussi, etc.)
        [Required]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // ? Méthode utilisée (MonCash, Virement bancaire, etc.)
        [Required]
        public PaymentMethod PaymentMethod { get; set; }
            
        // ? ID de la transaction externe (si applicable)
        [MaxLength(200)]
        public string? ExternalTransactionId { get; set; }

        // ? Liste des commandes liées à ce paiement
        public List<OrderProduct> OrderProducts { get; set; } = new();
        // Audit géré par BaseEntity
    }
}
