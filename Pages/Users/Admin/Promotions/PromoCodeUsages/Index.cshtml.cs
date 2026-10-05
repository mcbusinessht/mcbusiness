using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Promotions_PromoCodeUsages
{
    public class IndexModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public IndexModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IList<PromoCodeUsage> PromoCodeUsage { get;set; } = default!;

        public async Task OnGetAsync()
        {
            PromoCodeUsage = await _context.PromoCodeUsages
                .Include(p => p.PromoCode)
                .Include(p => p.UsedByUser).ToListAsync();
        }
    }
}
