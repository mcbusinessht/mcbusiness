using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;   

namespace MCBusiness.Pages_Users_Seller_Shipments_ShippingFees
{
    [Authorize]
    public class IndexModel : PageModel               
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager; 
        public IndexModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<ShippingFee> ShippingFee { get; set; } = new List<ShippingFee>();
        public SelectList Countries { get; set; }
        public SelectList Companies { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var company = await _context.Companies
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.City)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Country)
                .FirstOrDefaultAsync(c => c.SellerId == user.Id);
                
            if (company == null) return;

            ShippingFee = await _context.ShippingFees
                .Include(s => s.City)
                .Include(s => s.Company)
                    .ThenInclude(c => c.Addresses)
                        .ThenInclude(a => a.City)
                .Include(s => s.Country)
                .Include(s => s.Currency)
                .Include(s => s.Neighborhood)
                .Include(s => s.State)
                .Where(s => s.Company.Id == company.Id)
                .ToListAsync();

            // Initialiser les listes pour les filtres
            Countries = new SelectList(await _context.Countries.ToListAsync(), "Id", "Name");
            Companies = new SelectList(await _context.Companies.Where(c => c.Id == company.Id).ToListAsync(), "Id", "Name");
        }

        // Méthode utilitaire pour obtenir l'adresse principale d'une compagnie
        public string GetCompanyPrimaryAddress(Company company)
        {
            if (company?.Addresses == null || !company.Addresses.Any())
                return "Adresse non renseignée";

            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted)
                                ?? company.Addresses.First(a => !a.IsDeleted);

            return primaryAddress.FullAddress;
        }

        // Méthode utilitaire pour obtenir la ville d'une compagnie
        public string GetCompanyCity(Company company)
        {
            if (company?.Addresses == null || !company.Addresses.Any())
                return "Ville non renseignée";

            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted)
                                ?? company.Addresses.First(a => !a.IsDeleted);

            return primaryAddress.City?.Name ?? "Ville non renseignée";
        }

        // Méthode utilitaire pour obtenir le pays d'une compagnie
        public string GetCompanyCountry(Company company)
        {
            if (company?.Addresses == null || !company.Addresses.Any())
                return "Pays non renseigné";

            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted)
                                ?? company.Addresses.First(a => !a.IsDeleted);

            return primaryAddress.Country?.Name ?? "Pays non renseigné";
        }
    }
}
