using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace MCBusiness.Pages.Users.Seller.Shipments
{
    [Authorize(Roles = "Seller,Admin")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Shipment Shipment { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Shipment = await _context.Shipments
                .Include(s => s.DeliveryPerson)
                .Include(s => s.OrderProduct)
                    .ThenInclude(op => op.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(s => s.OrderProduct)
                    .ThenInclude(op => op.Order)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Shipment == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
