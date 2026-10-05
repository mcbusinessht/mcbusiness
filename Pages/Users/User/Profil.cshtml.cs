using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace MCBusiness.Pages.Users.User
{
    [Authorize]
    public class ProfilModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _user;

        public ProfilModel(AppDbContext context, UserManager<ApplicationUser> user)
        {
            _context = context;
            _user = user;
        }

        public ApplicationUser UserEntity { get; set; }

        [BindProperty]
        public IFormFile UploadedImage { get; set; }

        public string UploadedError { get; set; }
        public ApplicationUser CurrentUser { get; set; }
        public bool IsTwoFactorEnabled { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            UserEntity = await _user.GetUserAsync(User);
            IsTwoFactorEnabled = await _user.GetTwoFactorEnabledAsync(UserEntity);
            CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync()
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

                // Vérification de la taille maximale (2 Mo)
                const long maxSize = 2 * 1024 * 1024; // 2 Mo
                if (UploadedImage.Length > maxSize)
                {
                    UploadedError = "La taille de l'image ne doit pas dépasser 2 Mo.";
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

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (!string.IsNullOrEmpty(CurrentUser.ProfilePictureUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", CurrentUser.ProfilePictureUrl.TrimStart('/'));
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

                var fileName = $"{User.Identity.Name}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var image = Image.Load(UploadedImage.OpenReadStream()))
                {
                    // Découper un carré depuis le centre
                    var size = Math.Min(image.Width, image.Height);
                    var cropRectangle = new Rectangle((image.Width - size) / 2, (image.Height - size) / 2, size, size);

                    image.Mutate(x => x
                        .Crop(cropRectangle)
                        .Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Crop,
                            Size = new Size(300, 300) // Taille standard des photos de profil
                        }));

                    // Compresser à 50%
                    var encoder = new JpegEncoder
                    {
                        Quality = 50 // Compression à 50%
                    };

                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        image.Save(stream, encoder);
                    }
                }
                //Récupérer la taille du fichier compressé
                var fileInfo = new FileInfo(filePath);
                var CompressedImageSizeKb = fileInfo.Length / 1024.0; // Taille en Ko

                CurrentUser.ProfilePictureUrl = $"/uploads/{fileName}";
                var img = new SystemMedia{
                    Name=$"Profil de {CurrentUser.UserName}",
                    Alt="Photo de Profil",
                    Path= $"/uploads/{fileName}",
                    SizeInKb=$"{CompressedImageSizeKb} Kb"
                };
                _context.SystemMedias.Add(img);
                _context.Users.Update(CurrentUser);
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

        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            public string Adresse1 { get; set; }
            [Required]
            public string Ville { get; set; }
            [Required]
            public string Departement { get; set; }
            [Required]
            public string Pays { get; set; }
        }

        public async Task<IActionResult> OnPostEnableAsync()
        {
            var user = await _user.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await _user.IsEmailConfirmedAsync(user))
            {
                TempData["TwoFactorError"] =
                    "Confirmez votre adresse email avant d'activer l'authentification a deux facteurs.";
                return RedirectToPage("./Profil");
            }

            var result = await _user.SetTwoFactorEnabledAsync(user, true);
            if (!result.Succeeded)
            {
                TempData["TwoFactorError"] = "Impossible d'activer l'authentification a deux facteurs.";
            }

            return RedirectToPage("./Profil");
        }

        public async Task<IActionResult> OnPostDisableAsync()
        {
            var user = await _user.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _user.SetTwoFactorEnabledAsync(user, false);
            if (!result.Succeeded)
            {
                TempData["TwoFactorError"] = "Impossible de desactiver l'authentification a deux facteurs.";
            }

            return RedirectToPage("./Profil");
        }

        // public async Task<IActionResult> OnPostAdresseAsync()
        // {
        //     try
        //     {
        //         CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
        //         if (CurrentUser == null)
        //         {
        //             return RedirectToPage("/Account/Login");
        //         }

        //         // if (!ModelState.IsValid)
        //         // {
        //         //     var AdresseError2 = "Veuillez remplir tous les champs requis.";
        //         //     ModelState.AddModelError("AdresseErrorKey", AdresseError2);
        //         //     return Page();
        //         // }

                
        //         _context.Attach(CurrentUser).State = EntityState.Modified;
        //         await _context.SaveChangesAsync();

        //         return RedirectToPage("./Profil");
        //     }
        //     catch (Exception ex)
        //     {
        //         var AdresseError = "Une erreur s'est produite lors de la mise à jour des informations.";
        //         ModelState.AddModelError("AdresseErrorKey", AdresseError);
        //         Console.Error.WriteLine($"Erreur : {ex.Message}");
        //         return Page();
        //     }
        // }
    }
}
