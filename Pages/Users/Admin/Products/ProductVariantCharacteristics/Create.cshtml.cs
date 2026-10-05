using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products.ProductVariantCharacteristics
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            ViewData["CharacteristicId"] = new SelectList(_context.Characteristics, "Id", "Name");
            ViewData["CharacteristicValueId"] = new SelectList(_context.CharacteristicValues, "Id", "Value");
            ViewData["ProductVariantId"] = new SelectList(_context.ProductVariants, "Id", "Sku");
            return Page();
        }

        [BindProperty]
        public ProductVariantCharacteristic ProductVariantCharacteristic { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.ProductVariantCharacteristics.Add(ProductVariantCharacteristic);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
