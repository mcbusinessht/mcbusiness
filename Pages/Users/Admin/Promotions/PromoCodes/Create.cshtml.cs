using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Promotions_PromoCodes
{
    [Authorize(Roles = "Admin")]

    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            ViewData["CreatedByUserId"] = new SelectList(_context.Users.OrderBy(u => u.UserName), "Id", "UserName");
            ViewData["PromoCampaignId"] = new SelectList(_context.PromoCampaigns.OrderBy(c => c.Name), "Id", "Name");
            return Page();
        }

        [BindProperty]
        public PromoCode PromoCode { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.                        
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.PromoCodes.Add(PromoCode);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
