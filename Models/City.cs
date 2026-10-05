using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MCBusiness.Models;

namespace MCBusiness.Models
{
    public class City : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required, ForeignKey("State")]
        public int StateId { get; set; }
        public State State { get; set; }
        public ICollection<Neighborhood> Neighborhoods { get; set; } = new List<Neighborhood>();

    }
}
