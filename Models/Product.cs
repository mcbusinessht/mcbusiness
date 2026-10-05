using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Product : BaseEntity
    {

        [Required, MaxLength(255)]
        public string Name { get; set; }


        [Required,ForeignKey("Category")]
        public int CategoryId { get; set; }

        [ForeignKey("Brand")]
        public int? BrandId { get; set; }
        public Brand Brand { get; set; }

        [Required, ForeignKey("Company")]
        public int CompanyId { get; set; }
        public Company Company { get; set; }
        public int ProductView { get; set; } = 0;
        public string? Description {get; set;}
        public virtual Category Category { get; set; }
        // IsDeleted héritée de BaseEntity
        public bool IsPromoted { get; set; } = false;
        public ICollection<ProductVariant> Variants { get; set; }
        public ICollection<ProductReview> Reviews { get; set; }

        
    }
}
