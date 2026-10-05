using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products.ProductVariantCharacteristics
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<ProductVariantCharacteristic> ProductVariantCharacteristic { get; set; } = default!;

        public async Task OnGetAsync()
        {
            ProductVariantCharacteristic = await _context.ProductVariantCharacteristics
                .Include(p => p.Characteristic)
                .Include(p => p.CharacteristicValue)
                .Include(p => p.ProductVariant).ToListAsync();
        }
    }
}
