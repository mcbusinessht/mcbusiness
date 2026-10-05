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
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public PromoCodeUsage PromoCodeUsage { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            PromoCodeUsage = await _context.PromoCodeUsages
                .Include(p => p.PromoCode)
                .ThenInclude(p => p.PromoCampaign)
                .Include(p => p.UsedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (PromoCodeUsage == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
