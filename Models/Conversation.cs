using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;



namespace MCBusiness.Models
{
    public class Conversation : BaseEntity
    {
        [Required, ForeignKey("User1")]
        public string User1Id { get; set; } // Premier participant (ex: acheteur)
        [Required, ForeignKey("User2")]
        public string User2Id { get; set; } // Deuxième participant (ex: vendeur)
        // Navigation properties
        public ApplicationUser User1 { get; set; }
        public ApplicationUser User2 { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        
    }
}
