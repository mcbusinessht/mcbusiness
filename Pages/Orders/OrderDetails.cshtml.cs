using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Orders
{
    public class OrderDetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public OrderDetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Order Order { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Support des deux formats: /OrderDetails/2 et /OrderDetails?id=2
            if (!Id.HasValue && int.TryParse(Request.Query["id"], out int queryId))
            {
                Id = queryId;
            }

            if(!Id.HasValue){
                return Redirect("/MyCart");
            }
            var userId = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name)?.Id;
            
            Order = await _context.Orders
                .Include(o => o.Address)
                    .ThenInclude(c=>c.Country)
                .Include(o => o.Address)
                    .ThenInclude(c=>c.State)
                .Include(o => o.Address)
                    .ThenInclude(c=>c.City) 
                .Include(o => o.Products)
                    .ThenInclude(p => p.ProductVariant)  
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.Products)
                    .ThenInclude(p => p.ProductVariant)
                        .ThenInclude(pv => pv.ProductVariantCharacteristics)
                            .ThenInclude(pvc => pvc.Characteristic)
                .Include(o => o.Products)
                    .ThenInclude(p => p.ProductVariant)
                        .ThenInclude(pv => pv.ProductVariantCharacteristics)
                            .ThenInclude(pvc => pvc.CharacteristicValue)
                .FirstOrDefaultAsync(o => o.Id == Id.Value && o.UserId == userId);

            if (Order == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(int id)
        {
            var userId = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name)?.Id;
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order != null && (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Paid))
            {
                order.Status = OrderStatus.Cancelled;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("/Orders/MyOrders");
        }
    }
}
