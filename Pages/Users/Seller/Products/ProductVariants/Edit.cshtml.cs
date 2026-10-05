using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;


namespace MCBusiness.Pages.Users.Seller.Products.ProductVariants
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly OrderAmountService _orderAmountService;

        public EditModel(AppDbContext context, OrderAmountService orderAmountService)   
        {
            _context = context;
            _orderAmountService = orderAmountService;  
        }

        [BindProperty]
        public ProductVariant ProductVariant { get; set; } = default!;
        public Stock Stock { get; set; } = default!;
        public decimal PriceSnapshot { get; set; } = default!;
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var productvariant = await _context.ProductVariants.FirstOrDefaultAsync(m => m.Id == id);
            if (productvariant == null)
            {

                return NotFound();
            }
            Stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductVariantId == productvariant.Id);
            ProductVariant = productvariant;
            PriceSnapshot = ProductVariant.Price;
            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            // Charge l'entité suivie par le contexte
            var existing = await _context.ProductVariants
                                .Include(pv => pv.Stock)                          
                                .FirstOrDefaultAsync(pv => pv.Id == ProductVariant.Id);
            if (existing == null) return NotFound();
            PriceSnapshot = existing.Price;    

            // Mets à jour les propriétés
            existing.Price = ProductVariant.Price;
            existing.PriceSnapshot = PriceSnapshot;
            existing.Quantity = ProductVariant.Quantity;
            existing.IsAvailable = ProductVariant.IsAvailable;  
            existing.ShippingPackageType = ProductVariant.ShippingPackageType;
            existing.WeightKg = ProductVariant.WeightKg;
            existing.RequiresSpecialHandling = ProductVariant.RequiresSpecialHandling;
            existing.ShippingQuoteRequired = ProductVariant.ShippingQuoteRequired;
            // et le stock si besoin
            if (existing.Stock != null)
                existing.Stock.Quantity = ProductVariant.Quantity;

            // Mets à jour les commandes en attente
            await _orderAmountService.UpdatePendingOrdersForProductVariantAsync(existing.Id);

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }

        private bool ProductVariantExists(int id)
        {
            return _context.ProductVariants.Any(e => e.Id == id);
        }
    }
}
