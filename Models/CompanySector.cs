using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class CompanySector
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [ForeignKey("Company")]
        public int CompanyId { get; set; }
        public virtual Company Company { get; set; }
        
        [Required]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; }
        
        public DateTimeOffset AddedDate { get; set; } = DateTimeOffset.UtcNow;
        public bool IsPrimary { get; set; } = false; // Pour marquer le secteur principal
    }
}
