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
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Order> Order { get; set; } = default!;

        public async Task OnGetAsync()
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.SellerId == currentUser.Id);
            
            if (company == null)
            {
                Order = new List<Order>();
                return;
            }

            Order = await _context.Orders
                .Where(o => o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled)
                .Where(o => o.Products.Any(p => p.CompanyId == company.Id))
                .Include(o => o.Products.Where(p => p.CompanyId == company.Id))
                .Include(o => o.Address)
                .Include(o => o.User)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
        }
    }
}
