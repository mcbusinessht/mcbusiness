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
    public class DeleteModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DeleteModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
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

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var platformtransaction = await _context.PlatformTransactions.FindAsync(id);
            if (platformtransaction == null)
            {
                return NotFound();
            }

            PlatformTransaction = platformtransaction;
            ModelState.AddModelError(string.Empty,
                "Une écriture financière ne peut pas être supprimée. Elle doit être compensée par une écriture inverse.");
            return Page();
        }
    }
}
