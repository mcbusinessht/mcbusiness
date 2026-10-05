using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Sellers_CompanySectors
{
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.Type == CategoryType.CompanySector), "Id", "Name");
        ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Email");
            return Page();
        }

        [BindProperty]
        public CompanySector CompanySector { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.CompanySectors.Add(CompanySector);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
