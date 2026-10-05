using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Sellers_CompanySectors
{
    public class IndexModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public IndexModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IList<CompanySector> CompanySector { get;set; } = default!;

        public async Task OnGetAsync()
        {
            CompanySector = await _context.CompanySectors
                .Include(c => c.Category)
                .Include(c => c.Company).ToListAsync();
        }
    }
}
