using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Notices
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
            return Page();
        }

        [BindProperty]
        public Notice Notice { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // ?? FORCER UTC (OBLIGATOIRE POUR POSTGRES)
            Notice.StartDate = Notice.StartDate.ToUniversalTime();
            Notice.EndDate = Notice.EndDate.ToUniversalTime();

            _context.Notices.Add(Notice);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

    }
}
