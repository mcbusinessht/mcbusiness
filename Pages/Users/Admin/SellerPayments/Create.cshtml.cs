using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;

namespace MCBusiness.Pages_Users_Admin_SellerPayments
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            ViewData["CurrencyBase"] = new SelectList(_context.Currencies, "Id", "CurrencyBase");
            ViewData["OrderId"] = new SelectList(_context.Orders.Where(o => o.Status == OrderStatus.Delivered ), "Id", "Id");
            return Page();
        }

        [BindProperty]
        public SellerPayment SellerPayment { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.AddModelError(string.Empty,
                "La création manuelle est désactivée. Les paiements vendeurs doivent provenir d'une commande livrée.");
            OnGet();
            return Page();
        }
    }
}
