using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Neighborhood : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }
        [Required, ForeignKey("City")]
        public int CityId { get; set; }
        public City City { get; set; }
    }
}
