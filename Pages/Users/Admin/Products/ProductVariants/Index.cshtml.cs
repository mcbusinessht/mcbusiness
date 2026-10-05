using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products.ProductVariants
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<ProductVariant> ProductVariant { get; set; } = new List<ProductVariant>();

        public async Task OnGetAsync()
        {
            ProductVariant = await _context.ProductVariants
                .Include(pv => pv.Product) // Jointure avec Product
                    .ThenInclude(p => p.Category) // Jointure avec Category
                .Include(pv => pv.ProductVariantCharacteristics) // Jointure avec ProductVariantCharacteristic
                    .ThenInclude(pvc => pvc.Characteristic) // Jointure avec Characteristic
                .Include(pv => pv.ProductVariantCharacteristics)
                    .ThenInclude(pvc => pvc.CharacteristicValue) // Jointure avec CharacteristicValue
                .ToListAsync();
        }
    }
}
