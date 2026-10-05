using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.IdentityModel.Tokens;

namespace MCBusiness.Pages.Users.Seller.Products
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Product Product { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }
            else
            {
                Product = product;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                var ProductVariant = await _context.ProductVariants.Where(p => p.ProductId == product.Id).ToListAsync();
                if (!ProductVariant.IsNullOrEmpty())
                {
                    foreach (var item in ProductVariant)
                    {
                        var Images = await _context.ProductImages.Where(c => c.ProductVariantId == item.Id).ToListAsync();
                        if (!Images.IsNullOrEmpty())
                        {
                            foreach (var el in Images)
                            {
                                _context.ProductImages.Remove(el);
                                var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", el.Url.TrimStart('/'));
                                if (System.IO.File.Exists(oldFilePath))
                                {
                                    try
                                    {
                                        System.IO.File.Delete(oldFilePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.Error.WriteLine($"Erreur lors de la suppression de l'ancienne image : {ex.Message}");
                                    }
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                        var ProductVariantCharacteristic = await _context.ProductVariantCharacteristics.Where(c => c.ProductVariantId == item.Id).ToListAsync();
                        if (!ProductVariantCharacteristic.IsNullOrEmpty())
                        {
                            foreach (var ch in ProductVariantCharacteristic)
                            {
                                _context.ProductVariantCharacteristics.Remove(ch);
                                await _context.SaveChangesAsync();
                            }
                        }
                        

                        var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductVariantId == item.Id);
                       
                        if (stock != null)
                        {
                            _context.Stocks.Remove(stock);
                        }

                        
                        item.IsDeleted = true; // Marquer comme supprimé
                        _context.ProductVariants.Update(item);
                        await _context.SaveChangesAsync();
                    }
                }
                product.IsDeleted = true; // Marquer comme supprimé
                Product = product;
                _context.Products.Update(Product);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
