using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.Orders
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Order Order { get; set; } = default!;
        public List<OrderProduct> ListOrderProducts { get; set; } = new List<OrderProduct>();
        public Address Address { get; set; }
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.SellerId == user.Id);
            var order = await _context.Orders
                .Include(o => o.Currency)
                .Include(o => o.Address)
                .Include(o => o.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            Address =await _context.Addresses
                        .Include(c=>c.Country)
                        .Include(c=>c.State)
                        .Include(c=>c.City)
                        .FirstOrDefaultAsync(c=>c.Id == order.AddressId);
            if (order == null)
            {
                return NotFound();
            }
            else
            {
                Order = order;
                ListOrderProducts = await _context.OrderProducts
                .Where(o => o.CompanyId == company.Id && o.OrderId == order.Id)
                .Include(o => o.ProductVariant)
                .ThenInclude(op => op.Product)
                .Include(o => o.ProductVariant)
                .ThenInclude(op => op.Images)
                .Include(o => o.Shipment)
                .ToListAsync();
            }
            return Page();
        }
    }
}
