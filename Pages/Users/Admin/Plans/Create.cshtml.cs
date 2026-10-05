using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MCBusiness.Pages.Users.Admin.Plans
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            LoadCategories();
            return Page();
        }

        [BindProperty]
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        [Range(0, double.MaxValue, ErrorMessage = "Le prix doit être supérieur ou égal à 0.")]
        public decimal Price { get; set; }

        [BindProperty]
        [Range(1, 3650, ErrorMessage = "La durée doit être comprise entre 1 et 3650 jours.")]
        public int DurationInDays { get; set; }

        [BindProperty]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        [Range(1, int.MaxValue, ErrorMessage = "Veuillez sélectionner une catégorie.")]
        public int CategoryId { get; set; }

        [BindProperty]
        [Range(0, 1, ErrorMessage = "La commission doit être comprise entre 0 et 1. Exemple : 0.10 = 10%.")]
        public decimal AmbassadorCommissionPercentage { get; set; }

        [BindProperty]
        [Range(0, double.MaxValue, ErrorMessage = "Le plafond doit être supérieur ou égal à 0.")]
        public decimal Ceiling { get; set; }

        [BindProperty]
        public bool IsActive { get; set; } = true;

        [BindProperty]
        public IFormFile? UploadedImage { get; set; }

        [BindProperty]
        [Range(0, int.MaxValue, ErrorMessage = "Les points doivent être supérieurs ou égaux à 0.")]
        public int PointsGeneration1 { get; set; }

        [BindProperty]
        [Range(0, int.MaxValue, ErrorMessage = "Les points doivent être supérieurs ou égaux à 0.")]
        public int PointsGeneration2 { get; set; }

        [BindProperty]
        [Range(0, int.MaxValue, ErrorMessage = "Les points doivent être supérieurs ou égaux à 0.")]
        public int PointsGeneration3 { get; set; }

        [BindProperty]
        [Range(0, int.MaxValue, ErrorMessage = "Les points doivent être supérieurs ou égaux à 0.")]
        public int WelcomeBonusPoints { get; set; }

        [BindProperty]
        [Range(0, int.MaxValue, ErrorMessage = "Le plafond mensuel doit être supérieur ou égal à 0.")]
        public int MonthlyPointsLimit { get; set; }

        [BindProperty]
        public bool IsFreeplan { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (UploadedImage == null || UploadedImage.Length == 0)
            {
                ModelState.AddModelError(nameof(UploadedImage), "Veuillez sélectionner une image valide.");
            }

            if (!ModelState.IsValid)
            {
                LoadCategories();
                return Page();
            }

            try
            {
                var plan = new AmbassadorPlan
                {
                    Name = Name,
                    Price = Price,
                    DurationInDays = DurationInDays,
                    Description = Description,
                    CategoryId = CategoryId,
                    AmbassadorCommissionPercentage = AmbassadorCommissionPercentage,
                    Ceiling = Ceiling,
                    IsActive = IsActive,
                    CurrencyBase = 1,
                    PointsGeneration1 = PointsGeneration1,
                    PointsGeneration2 = PointsGeneration2,
                    PointsGeneration3 = PointsGeneration3,
                    WelcomeBonusPoints = WelcomeBonusPoints,
                    MonthlyPointsLimit = MonthlyPointsLimit,
                    IsFreeplan = IsFreeplan
                };

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "image");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(UploadedImage!.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await UploadedImage.CopyToAsync(stream);
                }

                plan.PathPicture = $"/image/{fileName}";

                _context.AmbassadorPlans.Add(plan);
                await _context.SaveChangesAsync();

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Une erreur est survenue lors de la création du plan : {ex.Message}");
                LoadCategories();
                return Page();
            }
        }

        private void LoadCategories()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.Type == CategoryType.Plan), "Id", "Name");
        }
    }
}
