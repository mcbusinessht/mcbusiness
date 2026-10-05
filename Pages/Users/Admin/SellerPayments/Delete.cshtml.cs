using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_SellerPayments
{
    public class DeleteModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DeleteModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public SellerPayment SellerPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sellerPayment = await _context.SellerPayments
                .Include(s => s.Company)
                .Include(s => s.Order)
                    .ThenInclude(s => s.Currency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sellerPayment == null)
            {
                return NotFound();
            }

            SellerPayment = sellerPayment;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sellerPayment = await _context.SellerPayments.FindAsync(id);
            if (sellerPayment == null)
            {
                return NotFound();
            }

            SellerPayment = sellerPayment;
            ModelState.AddModelError(string.Empty,
                sellerPayment.IsPaidToSeller
                    ? "Un paiement vendeur confirmé ne peut jamais être supprimé."
                    : "La suppression manuelle est désactivée afin de préserver la traçabilité financière.");
            return Page();
        }
    }
}
