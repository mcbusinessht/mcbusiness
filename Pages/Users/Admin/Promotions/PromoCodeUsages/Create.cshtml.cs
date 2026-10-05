using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;

namespace MCBusiness.Pages_Users_Admin_Promotions_PromoCodeUsages
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
            ViewData["PromoCodeId"] = new SelectList(_context.PromoCodes, "Id", "Code");
            ViewData["UsedByUserId"] = new SelectList(_context.Users, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public PromoCodeUsage PromoCodeUsage { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.PromoCodeUsages.Add(PromoCodeUsage);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
