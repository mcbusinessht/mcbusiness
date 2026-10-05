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
    public class IndexModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public IndexModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IList<SellerPayment> SellerPayment { get;set; } = default!;

        public async Task OnGetAsync()
        {
            SellerPayment = await _context.SellerPayments
                .Include(s => s.Company)
                    .ThenInclude(c => c.Addresses)
                        .ThenInclude(a => a.City)
                .Include(s => s.Company)
                    .ThenInclude(c => c.Addresses)
                        .ThenInclude(a => a.Country)
                .Include(s => s.Currency)
                .Include(s => s.Order)
                .OrderByDescending(s => s.PaymentDate)
                .ToListAsync();
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
    }
}
