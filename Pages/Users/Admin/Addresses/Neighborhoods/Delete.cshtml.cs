using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Addresses_Neighborhoods
{
    public class DeleteModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DeleteModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Neighborhood Neighborhood { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var neighborhood = await _context.Neighborhoods.FirstOrDefaultAsync(m => m.Id == id);

            if (neighborhood == null)
            {
                return NotFound();
            }
            else
            {
                Neighborhood = neighborhood;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var neighborhood = await _context.Neighborhoods.FindAsync(id);
            if (neighborhood != null)
            {
                Neighborhood = neighborhood;
                _context.Neighborhoods.Remove(Neighborhood);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
