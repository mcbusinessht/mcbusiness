using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public interface ICurrencyService
    {
        Task<decimal> ConvertFromHTG(decimal amount, string targetCurrencyCode);
        Task<Currency> GetCurrencyByCodeAsync(string code);
        Task<Currency> GetDefaultCurrencyAsync();
    }

    public class CurrencyService : ICurrencyService
    {
        private readonly AppDbContext _context;

        public CurrencyService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Convertit un montant depuis HTG (devise de base) vers une autre devise
        /// </summary>
        /// <param name="amount">Montant en HTG</param>
        /// <param name="targetCurrencyCode">Code de la devise cible (HTG, USD, EUR)</param>
        /// <returns>Montant converti arrondi à 2 décimales</returns>
        public async Task<decimal> ConvertFromHTG(decimal amount, string targetCurrencyCode)
        {
            if (string.IsNullOrEmpty(targetCurrencyCode) || targetCurrencyCode.ToUpper() == "HTG")
            {
                return Math.Round(amount, 2);
            }

            var currency = await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code.ToUpper() == targetCurrencyCode.ToUpper());

            if (currency == null)
            {
                // Si la devise n'existe pas, retourner le montant en HTG
                return Math.Round(amount, 2);
            }

            // Conversion: HTG / RateToHTG = devise cible
            // Exemple: 1000 HTG / 150 = 6.67 USD
            decimal convertedAmount = amount / currency.RateToBase;
            return Math.Round(convertedAmount, 2);
        }

        public async Task<Currency> GetCurrencyByCodeAsync(string code)
        {
            return await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code.ToUpper() == code.ToUpper());
        }

        public async Task<Currency> GetDefaultCurrencyAsync()
        {
            return await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsDefault) 
                ?? await _context.Currencies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Code == "HTG");
        }
    }
}
