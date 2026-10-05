using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Currencies
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            ViewData["Currencies"] = Enum.GetValues(typeof(CurrencyCode)).Cast<CurrencyCode>().Select(e => new SelectListItem
            {
                Value = e.ToString(),
                Text = e.ToString()
            });
            return Page();
        }

        [BindProperty]
        public Currency Currency { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Currencies.Add(Currency);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
