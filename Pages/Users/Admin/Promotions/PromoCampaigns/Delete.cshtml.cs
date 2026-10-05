using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Promotions_PromoCampaigns
{
    public class DeleteModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DeleteModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PromoCampaign PromoCampaign { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promocampaign = await _context.PromoCampaigns.FirstOrDefaultAsync(m => m.Id == id);

            if (promocampaign == null)
            {
                return NotFound();
            }
            else
            {
                PromoCampaign = promocampaign;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promocampaign = await _context.PromoCampaigns.FindAsync(id);
            if (promocampaign != null)
            {
                PromoCampaign = promocampaign;
                _context.PromoCampaigns.Remove(PromoCampaign);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
