using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Currency
    {
        [Key]
        public int Id { get; set; }
        
        [Required, MaxLength(3)]
        public string Code { get; set; } // HTG, USD, EUR
        
        [MaxLength(10)]
        public string Symbol { get; set; } // HTG, $, €
        
        public decimal RateToBase { get; set; } = 1.0m; // Taux de conversion vers HTG (base)
        
        public bool IsDefault { get; set; } = false;
        
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
        
        public bool IsDeleted { get; set; } = false;

        // Compatibility property (deprecated but kept for existing code)
        [NotMapped]
        public CurrencyCode CurrencyBase 
        { 
            get => Code switch
            {
                "HTG" => CurrencyCode.HTG,
                "USD" => CurrencyCode.USD,
                "EUR" => CurrencyCode.EUR,
                "CAD" => CurrencyCode.CAD,
                "DOP" => CurrencyCode.DOP,
                "GBP" => CurrencyCode.GBP,
                _ => CurrencyCode.HTG
            };
        }
    }
    
    public enum CurrencyCode
    {
        HTG,
        USD,
        CAD,
        DOP,
        EUR,
        GBP
    }
}
