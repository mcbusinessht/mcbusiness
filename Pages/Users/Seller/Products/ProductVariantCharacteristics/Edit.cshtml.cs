using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.Products.ProductVariantCharacteristics
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
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
            ProductVariantCharacteristic = productvariantcharacteristic;
            ViewData["CharacteristicId"] = new SelectList(_context.Characteristics, "Id", "Name");
            ViewData["CharacteristicValueId"] = new SelectList(_context.CharacteristicValues, "Id", "Value");
            ViewData["ProductVariantId"] = new SelectList(_context.ProductVariants, "Id", "Sku");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(ProductVariantCharacteristic).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductVariantCharacteristicExists(ProductVariantCharacteristic.ProductVariantId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ProductVariantCharacteristicExists(int id)
        {
            return _context.ProductVariantCharacteristics.Any(e => e.ProductVariantId == id);
        }
    }
}
