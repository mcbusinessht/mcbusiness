using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Address : BaseEntity
    {
        // Référence à l'utilisateur (maintenant nullable)
        [ForeignKey("User")]
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        // Nouvelle référence � la compagnie
        [ForeignKey("Company")]
        public int? CompanyId { get; set; }
        public virtual Company? Company { get; set; }

        // Type d'adresse pour distinguer l'usage
        [Required]
        public AddressType AddressType { get; set; } = AddressType.Personal;

        [Required, ForeignKey("Country")]
        public int CountryId { get; set; }
        public Country Country { get; set; }

        [Required, ForeignKey("State")]
        public int StateId { get; set; }
        public State State { get; set; }

        [Required, ForeignKey("City")]
        public int CityId { get; set; }
        public City City { get; set; }

        [ForeignKey("Neighborhood")]
        public int? NeighborhoodId { get; set; }
        public Neighborhood? Neighborhood { get; set; }

        [Required, MaxLength(200)]
        public string Line1 { get; set; }
        [MaxLength(200)]
        public string? Line2 { get; set; }

        [MaxLength(20)]
        public string? ZipCode { get; set; }
        
        public bool IsDefault { get; set; } = false;
        
        [NotMapped]
        public string FullAddress =>
            $"{Line1}"
            + (City != null ? $", {City.Name}" : "")
            + (State != null ? $", {State.Name}" : "")
            + (Country != null ? $", {Country.Name}" : "")
            + (!string.IsNullOrEmpty(ZipCode) ? $", {ZipCode}" : "");
    }

    public enum AddressType
    {
        Personal,    // Pour les utilisateurs
        Business,    // Pour les compagnies
        Billing,     // Pour la facturation
        Shipping     // Pour la livraison
    }
}
