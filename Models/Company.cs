using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Company : BaseEntity
    {
        
        [Required]
        [ForeignKey("Seller")]
        public string SellerId { get; set; }
        public virtual ApplicationUser Seller { get; set; }

        [Required]
        public string Name { get; set; }

        public string? Description { get; set; }

        [Required]
        public string PhoneNumber { get; set; }
        
        [Required]
        public string Email { get; set; }
        public string? WebSite { get; set; }
        public string? UrlLogo { get; set; }
        
        public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.UtcNow; 
        // IsDeleted héritée de BaseEntity
        public bool IsVerified { get; set; } = false;
        public bool IsBlocked { get; set; } = false;
        
        // Navigation property pour les secteurs
        public virtual ICollection<CompanySector> CompanySectors { get; set; } = new List<CompanySector>();
        
        // Navigation property pour les adresses
        public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();
        
        // Propriété utilitaire pour acc�der directement aux catégories
        [NotMapped]
        public IEnumerable<Category> Categories => CompanySectors?.Select(cs => cs.Category) ?? new List<Category>();
        
        // Propriété utilitaire pour l'adresse principale
        [NotMapped]
        public Address? PrimaryAddress => Addresses?.FirstOrDefault(a => a.IsDefault && !a.IsDeleted);
        
        // Propriétés de compatibilité pour les vues existantes
        [NotMapped]
        public string? Address => PrimaryAddress?.Line1;
        
        [NotMapped]
        public string? FullAddress => PrimaryAddress?.FullAddress;
            
        [NotMapped]
        public Category? Category => Categories?.FirstOrDefault();
        
        [NotMapped]
        public CompanySector? Sector => CompanySectors?.FirstOrDefault();
        
        [NotMapped]
        public City? City => PrimaryAddress?.City;
        
        [NotMapped]
        public State? State => PrimaryAddress?.State;
        
        [NotMapped]
        public Country? Country => PrimaryAddress?.Country;
    }
}
