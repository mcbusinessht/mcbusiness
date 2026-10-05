using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.UserActivityLogs
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public UserActivityLog UserActivityLog { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var useractivitylog = await _context.UserActivityLogs.FirstOrDefaultAsync(m => m.Id == id);
            if (useractivitylog == null)
            {
                return NotFound();
            }
            else
            {
                UserActivityLog = useractivitylog;
            }
            return Page();
        }
    }
}
