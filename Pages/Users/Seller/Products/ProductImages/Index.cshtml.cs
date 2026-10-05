using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Pages.Users.Seller.Products.ProductImages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }
        [BindProperty]
        public List<ProductImage> ProductImage { get; set; } = new List<ProductImage>();
        [BindProperty]
        public ProductVariant ProductVariant { get; set; } = default!;

        public IList<ProductVariantCharacteristic> ProductVariantCharacteristic { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id != null)
            {
                ProductVariant = await _context.ProductVariants.Include(c => c.Product).FirstOrDefaultAsync(p => p.Id == id);
                ProductImage = await _context.ProductImages
                    .Where(p => p.ProductVariantId == id)
                    .ToListAsync();
                ProductVariantCharacteristic = await _context.ProductVariantCharacteristics
                    .Where(ch => ch.ProductVariantId == ProductVariant.Id)
                    .Include(ch => ch.Characteristic)
                    .Include(chv => chv.CharacteristicValue)
                    .ToListAsync();

            }
            else
            {
                return Redirect("/Users/Seller/Products/Index");
            }
            return Page();
        }
    }
}
