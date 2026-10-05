using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Seller.Products.ProductImages
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public int? Id { get; set; }

        public IActionResult OnGet(int? id)
        {
            if (id != null)
            {
                Id = id;
                ViewData["ProductVariantId"] = new SelectList(_context.ProductVariants.Where(p => p.Id == id), "Id", "Sku");
            }
            else
            {
                ViewData["ProductVariantId"] = new SelectList(_context.ProductVariants, "Id", "Sku");
            }
            return Page();
        }

        [BindProperty]
        public ProductImage ProductImage { get; set; } = default!;

        [BindProperty]
        public IFormFile UploadedImage { get; set; }

        public string UploadedError { get; set; }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (Id != null)
            {
                ViewData["ProductVariantId"] = new SelectList(_context.ProductVariants.Where(p => p.Id == Id), "Id", "Sku");
            }
            else
            {
                ViewData["ProductVariantId"] = new SelectList(_context.ProductVariants, "Id", "Sku");
            }

            try
            {
                if (UploadedImage == null || UploadedImage.Length == 0)
                {
                    UploadedError = "Veuillez sélectionner une image valide.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                // Vérification de la taille maximale (1 Mo)
                const long maxSize = 1 * 1024 * 1024;
                if (UploadedImage.Length > maxSize)
                {
                    UploadedError = "La taille de l'image ne doit pas dépasser 1 Mo.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(UploadedImage.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    UploadedError = "Seuls les formats JPG, JPEG et PNG sont autorisés.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                // Vérification du nombre maximum de photos
                var existingImagesCount = await _context.ProductImages
                    .CountAsync(p => p.ProductVariantId == ProductImage.ProductVariantId);

                if (existingImagesCount >= 3)
                {
                    UploadedError = "Un maximum de 3 photos est autoris� par variant de produit.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Nettoyage de l'ancienne image si elle existe
                if (!string.IsNullOrEmpty(ProductImage.Url))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ProductImage.Url.TrimStart('/'));
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

                // Génération du nom de fichier
                var ProductVariant = _context.ProductVariants.FirstOrDefault(p => p.Id == ProductImage.ProductVariantId);
                var ProductName = ProductVariant.Sku;
                var fileName = $"{ProductName.Replace('/', '_')}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Sauvegarde directe du fichier sans traitement
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await UploadedImage.CopyToAsync(stream);
                }

                // Récupération de la taille du fichier
                var fileInfo = new FileInfo(filePath);
                var fileSizeKb = fileInfo.Length / 1024.0;

                ProductImage.Url = $"/uploads/{fileName}";

                var img = new SystemMedia
                {
                    Name = $"Produit {ProductName}",
                    Alt = $"Photo {ProductName}",
                    Path = $"/uploads/{fileName}",
                    SizeInKb = $"{fileSizeKb} Kb"
                };

                await _context.SystemMedias.AddAsync(img);
                await _context.ProductImages.AddAsync(ProductImage);
                await _context.SaveChangesAsync();

                return RedirectToPage("./Index", null, new { id = ProductImage.ProductVariantId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Une erreur est survenue lors de l'envoi de l'image.");
                Console.Error.WriteLine($"Erreur : {ex.Message}");
                return Page();
            }
        }
    }
}
