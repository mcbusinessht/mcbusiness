using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 
using Microsoft.EntityFrameworkCore; // <-- n?cessaire pour Include/ThenInclude

namespace MCBusiness.Pages.Users.Seller.Profil
{
    [Authorize(Roles = "Seller")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Pour l'affichage
        public Company Company { get; set; }

        // Pour le binding des champs modifiables
        [BindProperty]
        public CompanyUpdateModel UpdateData { get; set; }

        // Pour le logo
        [BindProperty]
        public IFormFile LogoFile { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            Company = await _context.Companies
                .Include(c => c.Seller)                     // si tu veux Seller
                .Include(c => c.CompanySectors)               // charger la relation mapp?e
                    .ThenInclude(cs => cs.Category)           // charger Category via CompanySector
                .Include(c => c.Addresses)                    // si tu affiches l'adresse / city
                    .ThenInclude(a => a.City)
                .FirstOrDefaultAsync(c => c.SellerId == userId);

            if (Company == null)
                return NotFound();

            UpdateData = new CompanyUpdateModel
            {
                PhoneNumber = Company.PhoneNumber,
                Email = Company.Email,
                WebSite = Company.WebSite,
                Description = Company.Description
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.SellerId == userId);

            if (company == null)
                return NotFound();

            // Si erreurs de validation, recharger Company pour la vue
            if (!ModelState.IsValid)
            {
                Company = company;
                return Page();
            }

            // Mettre ? jour les champs
            company.PhoneNumber = UpdateData.PhoneNumber;
            company.Email       = UpdateData.Email;
            company.WebSite     = UpdateData.WebSite;
            company.Description = UpdateData.Description;

            // Si un nouveau logo est upload?
            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "logos");
                Directory.CreateDirectory(uploadsDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await LogoFile.CopyToAsync(stream);

                company.UrlLogo = $"/uploads/logos/{fileName}";
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profil mis ? jour avec succ?s";
            return RedirectToPage();
        }
    }

    public class CompanyUpdateModel
    {
        [Required(ErrorMessage = "Le num?ro de t?l?phone est obligatoire")]
        [RegularExpression(@"^[0-9\+\-\s]{6,20}$", ErrorMessage = "Format de t?l?phone invalide")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "L'email est obligatoire")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public string Email { get; set; }

        [Url(ErrorMessage = "Format d'URL invalide")]
        public string WebSite { get; set; }

        [StringLength(500, ErrorMessage = "La description ne doit pas d?passer 500 caract?res")]
        public string Description { get; set; }
    }
}
