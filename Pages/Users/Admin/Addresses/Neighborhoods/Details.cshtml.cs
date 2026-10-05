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
    public class DetailsModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DetailsModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public Neighborhood Neighborhood { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var neighborhood = await _context.Neighborhoods.Where(m => m.Id == id).Include(m => m.City).FirstOrDefaultAsync();
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
    }
}
