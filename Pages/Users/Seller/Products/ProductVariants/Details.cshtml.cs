using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.Products.ProductVariants
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public ProductVariant ProductVariant { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productvariant = await _context.ProductVariants
                .Include(m => m.Product)
                .Include(m => m.Images)
                .Include(m => m.Currency)
                .Include(m => m.ProductVariantCharacteristics)
                .ThenInclude(ch => ch.Characteristic)
                .Include(m => m.ProductVariantCharacteristics)
                .ThenInclude(ch => ch.CharacteristicValue)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (productvariant == null)
            {
                return NotFound();
            }
            else
            {
                ProductVariant = productvariant;
            }
            return Page();
        }
    }
}
