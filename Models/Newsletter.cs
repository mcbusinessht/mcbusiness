using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class Newsletter
    {
        [Key]
        public int id{get;set;}
        [Required]
        [EmailAddress]
        public string Email{get;set;}
        public DateTimeOffset? DateAdded{get;set;} = DateTimeOffset.UtcNow;
        public bool IsDeleted{get;set;} = false;
        
    }
}
