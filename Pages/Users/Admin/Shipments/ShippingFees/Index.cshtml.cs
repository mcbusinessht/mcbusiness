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

namespace MCBusiness.Pages_Users_Admin_Shipments_ShippingFees
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
                .FirstOrDefaultAsync(c => c.SellerId == user.Id);
                
            if (company == null) return;

            ShippingFee = await _context.ShippingFees
                .Include(s => s.City)
                .Include(s => s.Company)
                .Include(s => s.Country)
                .Include(s => s.Currency)
                .Include(s => s.Neighborhood)
                .Include(s => s.State)
                .Where(s => s.Company.Id == company.Id)
                .ToListAsync();

            // Initialiser les listes pour les filtres
            Countries = new SelectList(await _context.Countries.ToListAsync(), "Id", "Name");
            Companies = new SelectList(await _context.Companies.ToListAsync(), "Id", "Name");
        }
    }
}
