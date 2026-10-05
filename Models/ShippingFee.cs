using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MCBusiness.Models
{ 
    public class ShippingFee : BaseEntity
    {
        [Required, ForeignKey("Company")]
        public int CompanyId { get; set; }
        [ValidateNever]
        public Company Company { get; set; }

        [Required, ForeignKey("Country")]
        public int CountryId { get; set; }
        [ValidateNever]
        public Country Country { get; set; }

        [Required, ForeignKey("State")]
        public int StateId { get; set; }
        [ValidateNever]
        public State State { get; set; }

        [Required, ForeignKey("City")]
        public int CityId { get; set; }
        [ValidateNever]
        public City City { get; set; }

        [ForeignKey("Neighborhood")]
        public int? NeighborhoodId { get; set; }
        [ValidateNever]
        public Neighborhood? Neighborhood { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        public ShippingPackageType PackageType { get; set; } = ShippingPackageType.Any;

        [Range(0, double.MaxValue)]
        public decimal BasePrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal AdditionalItemPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal AdditionalUnitPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? PricePerKg { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? MinPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? MaxPrice { get; set; }

        public bool QuoteRequired { get; set; } = false;
        public bool IsActive { get; set; } = true;

        [Required, ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        [ValidateNever]
        public Currency Currency { get; set; }

    }
}

