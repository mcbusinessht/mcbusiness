using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Sellers_CompanySectors
{
    public class DetailsModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DetailsModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public CompanySector CompanySector { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var companysector = await _context.CompanySectors.FirstOrDefaultAsync(m => m.Id == id);
            if (companysector == null)
            {
                return NotFound();
            }
            else
            {
                CompanySector = companysector;
            }
            return Page();
        }
    }
}
