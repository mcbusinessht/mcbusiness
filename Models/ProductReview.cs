using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MCBusiness.Models
{
    public class ProductReview
    {
        public int Id { get; set; }
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        [ForeignKey("User")]
        public string UserId { get; set; }
        public int Rate { get; set; } // 1 à 5 étoiles
        public string Comment { get; set; }
        public DateTimeOffset DateReview { get; set; }

        public virtual Product Product { get; set; }
        public virtual ApplicationUser User { get; set; }
        public bool IsDeleted { get; set; } = false; 
        public bool? IsApproved { get; set; } = false;
        public DateTimeOffset? DateAdded { get; set; } = DateTimeOffset.UtcNow;
        public int? HelpfulVotes { get; set; } = 0;
        public int? UnhelpfulVotes { get; set; } = 0;    

    }

}
