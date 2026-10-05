using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class Brand : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [MaxLength(500)]
        public string? Description { get; set; }
        [MaxLength(500)]
        public string? UrlLogo { get; set; }
        [MaxLength(300)]
        public string? WebSite { get; set; }
        [MaxLength(100)]
        public string? Country { get; set; }
    }
}
