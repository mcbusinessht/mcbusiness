using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products.ProductVariants
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

            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name");
            ViewData["Characteristic"] = new SelectList(_context.Characteristics, "Id", "Name");
            return Page();
        }

        [BindProperty]
        public ProductVariant ProductVariant { get; set; } = new();

        [BindProperty]
        public List<ProductVariantCharacteristic> ProductVariantCharacteristics { get; set; } = new();

        [BindProperty]
        public string SelectedCharacteristics { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.ProductVariants.Add(ProductVariant);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Product---------------------------------------------{SelectedCharacteristics}");
            if (!string.IsNullOrEmpty(SelectedCharacteristics))
            {
                var characteristicPairs = SelectedCharacteristics.Split(",");
                foreach (var pair in characteristicPairs)
                {
                    var ids = pair.Split("-");
                    var characteristicId = int.Parse(ids[0]);
                    var characteristicValueId = int.Parse(ids[1]);

                    ProductVariantCharacteristics.Add(new ProductVariantCharacteristic
                    {
                        ProductVariantId = ProductVariant.Id,
                        CharacteristicId = characteristicId,
                        CharacteristicValueId = characteristicValueId
                    });
                    Console.WriteLine($"Prosuct---------------------------{pair}");
                }

                _context.ProductVariantCharacteristics.AddRange(ProductVariantCharacteristics);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }

        public JsonResult OnGetCharacteristicValues(int characteristicId)
        {
            var values = _context.CharacteristicValues
                .Where(v => v.CharacteristicId == characteristicId)
                .Select(v => new { v.Id, v.Value })
                .ToList();

            return new JsonResult(values);
        }
    }
}
