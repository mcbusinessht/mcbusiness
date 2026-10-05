using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_SellerPayments
{
    public class DetailsModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DetailsModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public SellerPayment SellerPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sellerPayment = await _context.SellerPayments
                .Include(m => m.Company)
                    .ThenInclude(c => c.Addresses)
                        .ThenInclude(a => a.City)
                .Include(m => m.Company)
                    .ThenInclude(c => c.Addresses)
                        .ThenInclude(a => a.Country)
                .Include(m => m.Order)
                    .ThenInclude(o => o.Currency)
                .Include(m => m.Currency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sellerPayment == null)
            {
                return NotFound();
            }

            SellerPayment = sellerPayment;
            return Page();
        }

        // Méthode utilitaire pour obtenir l'adresse principale d'une compagnie
        public string GetCompanyPrimaryAddress(Company company)
        {
            if (company?.Addresses == null || !company.Addresses.Any())
                return "Adresse non renseignée";

            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted)
                                ?? company.Addresses.FirstOrDefault(a => !a.IsDeleted);

            if (primaryAddress == null)
                return "Adresse non renseignée";

            return primaryAddress.FullAddress;
        }

        // Méthode utilitaire pour obtenir la ville d'une compagnie
        public string GetCompanyCity(Company company)
        {
            if (company?.Addresses == null || !company.Addresses.Any())
                return "Ville non renseignée";

            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted)
                                ?? company.Addresses.FirstOrDefault(a => !a.IsDeleted);

            if (primaryAddress == null)
                return "Ville non renseignée";

            return primaryAddress.City?.Name ?? "Ville non renseignée";
        }

        // Méthode utilitaire pour obtenir le pays d'une compagnie
        public string GetCompanyCountry(Company company)
        {
            if (company?.Addresses == null || !company.Addresses.Any())
                return "Pays non renseigné";

            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted)
                                ?? company.Addresses.FirstOrDefault(a => !a.IsDeleted);

            if (primaryAddress == null)
                return "Pays non renseigné";

            return primaryAddress.Country?.Name ?? "Pays non renseigné";
        }
    }
}
