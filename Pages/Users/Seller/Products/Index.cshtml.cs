using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Pages.Users.Seller.Products
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Product> Product { get; set; } = default!;
        [BindProperty]
        public Company Company { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (CurrentUser == null)
            {
                return Redirect("/Users/Seller/Dashboard");
            }
            Company = _context.Companies.FirstOrDefault(c => c.SellerId == CurrentUser.Id);
            if (Company == null)
            {
                return Redirect("/Users/Seller/NoCompany");
            }
            Product = await _context.Products
                .Where(p => p.CompanyId == Company.Id && !p.IsDeleted) // Ajout du filtre pour les produits non supprimés
                .Include(p => p.Category)
                .Include(p => p.Company)
                .ToListAsync();


            return Page();
        }
    }
}
