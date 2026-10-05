using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Transactions_PlatformTransactions
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
        ViewData["AmbassadorSubscriptionId"] = new SelectList(_context.AmbassadorSubscriptions, "Id", "UserId");
        ViewData["EmployeeId"] = new SelectList(_context.Employees, "Id", "UserId");
        ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id");
        ViewData["AmbassadorSubscriptionId"] = new SelectList(_context.AmbassadorSubscriptions, "Id", "Id");
        ViewData["SupplierId"] = new SelectList(_context.Users, "Id", "Id");
        ViewData["WalletId"] = new SelectList(_context.Wallets, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public PlatformTransaction PlatformTransaction { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.AddModelError(string.Empty,
                "La création manuelle d'une écriture plateforme est désactivée. Utilisez le service financier afin de conserver la contrepartie portefeuille.");
            OnGet();
            return Page();
        }
    }
}
