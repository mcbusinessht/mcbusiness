using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.IdentityModel.Tokens;

namespace MCBusiness.Pages.Users.Seller.Products.ProductVariants
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ProductVariant ProductVariant { get; set; } = default!;

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
            else
            {
                ProductVariant = productvariant;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productvariant = await _context.ProductVariants.FindAsync(id);                    
            
            if (productvariant == null)
            {
                return NotFound();
            }

            // Supprimer les caract�ristiques
            var productVariantCaracteristics = _context.ProductVariantCharacteristics
                .Where(p => p.ProductVariantId == productvariant.Id);

            foreach (var item in productVariantCaracteristics)
            {
                _context.ProductVariantCharacteristics.Remove(item);
            }

            // Supprimer les images
            var productImages = await _context.ProductImages
                .Where(p => p.ProductVariantId == productvariant.Id)
                .ToListAsync();

            foreach (var img in productImages)
            {
                _context.ProductImages.Remove(img);
                if (!string.IsNullOrEmpty(img.Url))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.Url.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Erreur lors de la suppression de l'image : {ex.Message}");
                        }
                    }
                }
            }

            // Supprimer le stock s'il existe
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductVariantId == productvariant.Id);
            if (stock != null)
            {
                _context.Stocks.Remove(stock);
            }

            // Marquer le produit comme supprimé
            productvariant.IsDeleted = true;
            _context.ProductVariants.Update(productvariant);

            await _context.SaveChangesAsync();
            
            var prodV = await _context.ProductVariants.Where(p=> !p.IsDeleted &&  p.ProductId == productvariant.ProductId).ToListAsync();
            if(prodV == null){
                 var prod = await _context.Products.FindAsync(productvariant.ProductId);
                 prod.IsDeleted = true;
                 _context.Products.Update(prod);
                 _context.SaveChanges();
            }

            return RedirectToPage("./Index");  
        }

    }
}
