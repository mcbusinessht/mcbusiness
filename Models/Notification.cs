using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace MCBusiness.Models
{
    public class Notification : BaseEntity
    {
        [Required, ForeignKey("User")]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        [MaxLength(200)]
        public string? Title { get; set; }
        [Required, MaxLength(1000)]
        public string Message { get; set; }
        [MaxLength(50)]
        public string NotificationType { get; set; } = "general";
        public bool IsRead { get; set; } = false;
        // CreatedAt et IsDeleted héritées de BaseEntity
    }
}
