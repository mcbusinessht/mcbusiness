using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.Products.ProductImages
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ProductImage ProductImage { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productimage = await _context.ProductImages.FirstOrDefaultAsync(m => m.Id == id);

            if (productimage == null)
            {
                return NotFound();
            }
            else
            {
                ProductImage = productimage;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productimage = await _context.ProductImages.FindAsync(id);
            if (productimage != null)
            {
                if (!string.IsNullOrEmpty(productimage.Url))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", productimage.Url.TrimStart('/'));
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
                ProductImage = productimage;
                _context.ProductImages.Remove(ProductImage);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index", null, new { id = productimage.ProductVariantId });
        }
    }
}
