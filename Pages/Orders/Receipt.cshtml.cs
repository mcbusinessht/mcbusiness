using System.Globalization;
using System.Security.Claims;
using MCBusiness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Orders
{
    public class ReceiptModel : PageModel
    {
        private readonly AppDbContext _context;

        public ReceiptModel(AppDbContext context)
        {
            _context = context;
        }

        public Order Order { get; set; }
        public CultureInfo Culture { get; } = new("fr-HT");

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login");
            }

            Order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Address)
                    .ThenInclude(a => a.City)
                .Include(o => o.Address)
                    .ThenInclude(a => a.State)
                .Include(o => o.Address)
                    .ThenInclude(a => a.Country)
                .Include(o => o.Payment)
                .Include(o => o.Products)
                    .ThenInclude(op => op.Company)
                .Include(o => o.Products)
                    .ThenInclude(op => op.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId && !o.IsDeleted);

            if (Order == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
