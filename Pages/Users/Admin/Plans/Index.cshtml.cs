using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Plans
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<AmbassadorPlan> Plan { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.AmbassadorPlans != null)
            {
                // OPTIMISÉ: AsNoTracking + Include Category
                Plan = await _context.AmbassadorPlans
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .OrderBy(p => p.Price)
                    .ToListAsync();
            }
        }
    }
}
