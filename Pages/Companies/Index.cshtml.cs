using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Companies
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Company> Company { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Company = await _context.Companies
                .Include(c => c.CompanySectors)
                    .ThenInclude(cs => cs.Category)
                .Include(c => c.Seller)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.City)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Country)
                .Where(c => !c.IsDeleted && !c.IsBlocked)
                .ToListAsync();
        }
    }
}
