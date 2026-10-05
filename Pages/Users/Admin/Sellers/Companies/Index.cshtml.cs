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
                .Select(c => new Company {
                    Id = c.Id,
                    Name = c.Name,
                    CompanySectors = c.CompanySectors, // EF va traduire et charger
                    Addresses = c.Addresses
                }).ToListAsync();

        }
    }
}
