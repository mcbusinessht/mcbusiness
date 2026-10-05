using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    
    public class Category : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required]
        public CategoryType Type { get; set; }
        public int? ParentCategoryId { get; set; }
        [MaxLength(500)]
        public string? UrlLogo { get; set; }
        [ForeignKey("ParentCategoryId")]
        public Category? ParentCategory { get; set; }
        // IsDeleted héritée de BaseEntity - utiliser pour soft delete
        public bool IsActive { get; set; } = true;
        // Navigation property pour les compagnies
        public virtual ICollection<CompanySector> CompanySectors { get; set; } = new List<CompanySector>();
        public ICollection<Category> SubCategories { get; set; }
        public ICollection<Product> Products { get; set; }
    }

    public enum CategoryType{
        Product,
        Service,
        Plan,
        CompanySector
    }
}
