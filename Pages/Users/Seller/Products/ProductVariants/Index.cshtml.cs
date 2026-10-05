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
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<ProductVariant> ProductVariant { get; set; } = new List<ProductVariant>();
        public ApplicationUser CurrentUser { get; set; }
        public int idProduct { get; set; } = 0;

        public async Task OnGetAsync(int? id)
        {

            CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            Company Company = _context.Companies.FirstOrDefault(c => c.SellerId == CurrentUser.Id);
            if (Company != null)
            {
                if (id.HasValue && id.Value > 0)
                {
                    idProduct = (int)id.Value;  
                    ProductVariant = await _context.ProductVariants.Where(p => p.ProductId == id && p.IsDeleted == false) // Ajout du filtre pour les variants non supprimés
                    .Include(pv => pv.Product) // Jointure avec Product  
                        .ThenInclude(p => p.Category) // Jointure avec Category
                    .Include(pv => pv.Currency)
                    .Include(pv => pv.Stock)
                    .Include(pv => pv.Images)
                    .Include(pv => pv.PriceBreaks)
                    .Include(pv => pv.ProductVariantCharacteristics) // Jointure avec ProductVariantCharacteristic
                        .ThenInclude(pvc => pvc.Characteristic) // Jointure avec Characteristic
                    .Include(pv => pv.ProductVariantCharacteristics)
                        .ThenInclude(pvc => pvc.CharacteristicValue).Where(c => c.Product.CompanyId == Company.Id  ) // Jointure avec CharacteristicValue
                    .ToListAsync();
                }
                else
                {
                    ProductVariant = await _context.ProductVariants
                    .Include(pv => pv.Product) // Jointure avec Product
                        .ThenInclude(p => p.Category) // Jointure avec Category
                    .Include(pv => pv.Currency)
                    .Include(pv => pv.Stock)
                    .Include(pv => pv.Images)
                    .Include(pv => pv.PriceBreaks)
                    .Include(pv => pv.ProductVariantCharacteristics) // Jointure avec ProductVariantCharacteristic
                        .ThenInclude(pvc => pvc.Characteristic) // Jointure avec Characteristic
                    .Include(pv => pv.ProductVariantCharacteristics)
                        .ThenInclude(pvc => pvc.CharacteristicValue)
                    .Where(p => p.IsDeleted == false && p.Product.CompanyId == Company.Id) // <== ici
                    .ToListAsync();
                }
            }

        }
    }
}
