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
    public class DeleteModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DeleteModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PromoCodeUsage PromoCodeUsage { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promocodeusage = await _context.PromoCodeUsages.FirstOrDefaultAsync(m => m.Id == id);

            if (promocodeusage == null)
            {
                return NotFound();
            }
            else
            {
                PromoCodeUsage = promocodeusage;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promocodeusage = await _context.PromoCodeUsages.FindAsync(id);
            if (promocodeusage != null)
            {
                PromoCodeUsage = promocodeusage;
                _context.PromoCodeUsages.Remove(PromoCodeUsage);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
