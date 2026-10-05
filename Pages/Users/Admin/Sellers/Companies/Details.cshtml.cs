using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Sellers.Companies
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;    

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Company Company { get; set; } = default!;

        
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var company = await _context.Companies
                .AsNoTracking() // lecture seule, am?liore perf
                .Include(c => c.Seller)
                .Include(c => c.CompanySectors)
                    .ThenInclude(cs => cs.Category)
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.Country)
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.State)
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.City)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (company == null) return NotFound();

            Company = company; // ta propri?t? [NotMapped] Category sera d?riv?e de CompanySectors charg?s
            return Page();
        }
    }
}
