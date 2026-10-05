using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Transactions_PlatformTransactions
{
    public class DetailsModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DetailsModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public PlatformTransaction PlatformTransaction { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var platformtransaction = await _context.PlatformTransactions.FirstOrDefaultAsync(m => m.Id == id);
            if (platformtransaction == null)
            {
                return NotFound();
            }
            else
            {
                PlatformTransaction = platformtransaction;
            }
            return Page();
        }
    }
}
