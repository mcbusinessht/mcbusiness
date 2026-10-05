using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Seller_Shipments_ShippingFees
{
    public class DeleteModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public DeleteModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ShippingFee ShippingFee { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shippingfee = await _context.ShippingFees
                .Include(s => s.Company)
                .Include(s => s.Country)
                .Include(s => s.State)
                .Include(s => s.City)
                .Include(s => s.Neighborhood)
                .Include(s => s.Currency)
                .FirstOrDefaultAsync(m => m.Id == id);          

            if (shippingfee == null)
            {
                return NotFound();
            }
            else
            {
                ShippingFee = shippingfee;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shippingfee = await _context.ShippingFees.FindAsync(id);
            if (shippingfee != null)
            {
                ShippingFee = shippingfee;
                _context.ShippingFees.Remove(ShippingFee);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
