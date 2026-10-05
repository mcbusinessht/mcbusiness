using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.Products.ProductVariantCharacteristics
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public ProductVariantCharacteristic ProductVariantCharacteristic { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productvariantcharacteristic = await _context.ProductVariantCharacteristics.FirstOrDefaultAsync(m => m.ProductVariantId == id);
            if (productvariantcharacteristic == null)
            {
                return NotFound();
            }
            else
            {
                ProductVariantCharacteristic = productvariantcharacteristic;
            }
            return Page();
        }
    }
}
