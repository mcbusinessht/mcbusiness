using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace MCBusiness.Pages.Users.Admin.Medias
{
    public class CreateModel : PageModel
    {
        private readonly ILogger<CreateModel> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _user;

        public CreateModel(ILogger<CreateModel> logger, AppDbContext context, UserManager<ApplicationUser> user)
        {
            _logger = logger;
            _context = context;
            _user = user;
        }

        public ApplicationUser UserEntity { get; set; }

        [BindProperty]
        public IFormFile UploadedImage { get; set; }

        public string UploadedError { get; set; }
        [BindProperty]
        public string NameM { get; set; }

        [BindProperty]
        public string AltM { get; set; }

        public SystemMedia img { get; set; }

        public ApplicationUser CurrentUser { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (User.Identity == null || string.IsNullOrEmpty(User.Identity.Name))
                {
                    UploadedError = "Utilisateur non connecté.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return RedirectToPage("/Account/Login");
                }

                CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
                if (CurrentUser == null)
                {
                    UploadedError = "Utilisateur non trouvé.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                if (UploadedImage == null || UploadedImage.Length == 0)
                {
                    UploadedError = "Veuillez sélectionner une image valide.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                // Vérification de la taille maximale (10 Mo)
                const long maxSize = 10 * 1024 * 1024; // 10 Mo
                if (UploadedImage.Length > maxSize)
                {
                    UploadedError = "La taille de l'image ne doit pas dépasser 10 Mo.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/image");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = UploadedImage.FileName;
                var fileExtension = Path.GetExtension(fileName).ToLower();

                // Vérification des formats autorisés
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                if (!allowedExtensions.Contains(fileExtension))
                {
                    UploadedError = "Format de fichier non supporté. Formats autorisés : .jpg, .jpeg, .png, .gif.";
                    ModelState.AddModelError("UploadedErrorKey", UploadedError);
                    return Page();
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                // Sauvegarder le fichier tel quel sans compression
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await UploadedImage.CopyToAsync(stream);
                }

                // Récupérer la taille du fichier
                var fileInfo = new FileInfo(filePath);
                var fileSizeKb = fileInfo.Length / 1024.0; // Taille en Ko

                // Ajouter l'image dans la base de données
                img = new SystemMedia
                {
                    Name = NameM,
                    Alt = AltM,
                    Path = $"/image/{fileName}",
                    SizeInKb = $"{fileSizeKb} Kb"
                };
                _context.SystemMedias.Update(img);
                await _context.SaveChangesAsync();

                return RedirectToPage();
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
  
