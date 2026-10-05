using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class Country : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required, MaxLength(5)]
        public string CountryCode { get; set; }
        public ICollection<State> States { get; set; } = new List<State>(); 

    }
}
