using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Transactions_PlatformTransactions
{
    public class EditModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public EditModel(MCBusiness.Models.AppDbContext context)
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

            var platformtransaction =  await _context.PlatformTransactions.FirstOrDefaultAsync(m => m.Id == id);
            if (platformtransaction == null)
            {
                return NotFound();
            }
            PlatformTransaction = platformtransaction;
           ViewData["AmbassadorSubscriptionId"] = new SelectList(_context.AmbassadorSubscriptions, "Id", "UserId");
           ViewData["EmployeeId"] = new SelectList(_context.Employees, "Id", "UserId");
           ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id");
           ViewData["AmbassadorSubscriptionId"] = new SelectList(_context.AmbassadorSubscriptions, "Id", "Id");
           ViewData["SupplierId"] = new SelectList(_context.Users, "Id", "Id");
           ViewData["WalletId"] = new SelectList(_context.Wallets, "Id", "Id");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _context.PlatformTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == PlatformTransaction.Id);
            if (existing == null)
            {
                return NotFound();
            }

            PlatformTransaction = existing;
            ModelState.Clear();
            ModelState.AddModelError(string.Empty,
                "Une écriture financière validée ne peut pas être modifiée. Créez une écriture inverse via le service financier.");
            return Page();
        }

        private bool PlatformTransactionExists(int id)
        {
            return _context.PlatformTransactions.Any(e => e.Id == id);
        }
    }
}
