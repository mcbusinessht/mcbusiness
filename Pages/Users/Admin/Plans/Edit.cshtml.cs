using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Admin.Plans
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public AmbassadorPlan Plan { get; set; } = default!;

        [BindProperty]
        public IFormFile? UploadedImage { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var plan = await _context.AmbassadorPlans
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (plan == null)
            {
                return NotFound();
            }

            Plan = plan;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Plan.Category");
            ModelState.Remove("Plan.Currency");
            ModelState.Remove("UploadedImage");

            if (Plan.Price < 0)
            {
                ModelState.AddModelError("Plan.Price", "Le prix doit être supérieur ou égal à 0.");
            }

            if (Plan.AmbassadorCommissionPercentage < 0 || Plan.AmbassadorCommissionPercentage > 1)
            {
                ModelState.AddModelError("Plan.AmbassadorCommissionPercentage", "La commission doit être comprise entre 0 et 1. Exemple : 0.10 = 10%.");
            }

            if (Plan.DurationInDays < 1 || Plan.DurationInDays > 3650)
            {
                ModelState.AddModelError("Plan.DurationInDays", "La durée doit être comprise entre 1 et 3650 jours.");
            }

            if (!ModelState.IsValid)
            {
                await ReloadPlanReferencesAsync();
                return Page();
            }

            if (UploadedImage != null && UploadedImage.Length > 0)
            {
                try
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "image");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    if (!string.IsNullOrWhiteSpace(Plan.PathPicture))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", Plan.PathPicture.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(UploadedImage.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await UploadedImage.CopyToAsync(stream);
                    }

                    Plan.PathPicture = $"/image/{fileName}";
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Erreur lors du traitement de l'image : {ex.Message}");
                    await ReloadPlanReferencesAsync();
                    return Page();
                }
            }

            var planToUpdate = await _context.AmbassadorPlans.FirstOrDefaultAsync(p => p.Id == Plan.Id);
            if (planToUpdate == null)
            {
                return NotFound();
            }

            planToUpdate.Name = Plan.Name;
            planToUpdate.Description = Plan.Description;
            planToUpdate.Price = Plan.Price;
            planToUpdate.DurationInDays = Plan.DurationInDays;
            planToUpdate.Ceiling = Plan.Ceiling;
            planToUpdate.CategoryId = Plan.CategoryId;
            planToUpdate.CurrencyBase = Plan.CurrencyBase > 0 ? Plan.CurrencyBase : planToUpdate.CurrencyBase;
            planToUpdate.AmbassadorCommissionPercentage = Plan.AmbassadorCommissionPercentage;
            planToUpdate.PointsGeneration1 = Plan.PointsGeneration1;
            planToUpdate.PointsGeneration2 = Plan.PointsGeneration2;
            planToUpdate.PointsGeneration3 = Plan.PointsGeneration3;
            planToUpdate.WelcomeBonusPoints = Plan.WelcomeBonusPoints;
            planToUpdate.MonthlyPointsLimit = Plan.MonthlyPointsLimit;
            planToUpdate.IsFreeplan = Plan.IsFreeplan;
            planToUpdate.IsActive = Plan.IsActive;
            planToUpdate.PathPicture = Plan.PathPicture;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.AmbassadorPlans.Any(e => e.Id == Plan.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToPage("./Index");
        }

        private async Task ReloadPlanReferencesAsync()
        {
            Plan.Category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == Plan.CategoryId);
        }
    }
}
