using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class State : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required, ForeignKey("Country")]
        public int CountryId { get; set; }
        public Country Country { get; set; }
        public ICollection<City> Cities { get; set; } = new List<City>();
    }
}
