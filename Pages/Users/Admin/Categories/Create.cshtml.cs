using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages_Users_Admin_Categories
{
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;
        private readonly IAuditService _auditService;

        public CreateModel(MCBusiness.Models.AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // Au lieu de binder l'objet Category complet, binder les propriétés individuellement
        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public CategoryType Type { get; set; }

        [BindProperty]
        public int? ParentCategoryId { get; set; }

        [BindProperty]
        public string? UrlLogo { get; set; }

        // IsDeleted supprimé - géré automatiquement par BaseEntity

        public IActionResult OnGet()
        {
            ViewData["ParentCategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["ParentCategoryId"] = new SelectList(_context.Categories, "Id", "Name");
                return Page();
            }

            // Créer l'objet Category avec audit automatique
            var category = new Category
            {
                Name = Name,
                Type = Type,
                ParentCategoryId = ParentCategoryId,
                UrlLogo = UrlLogo,
                IsActive = true // Nouvelle catégorie active par défaut
                // DateAdded supprimé - remplacé par CreatedAt de BaseEntity
                // IsDeleted géré automatiquement par BaseEntity
            };

            // Appliquer l'audit automatique avec versioning
            var userId = _auditService.GetCurrentUserId(User);
            _auditService.SetAuditFields(category, userId);

            try
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erreur lors de l'enregistrement: " + ex.Message);
                ViewData["ParentCategoryId"] = new SelectList(_context.Categories, "Id", "Name");
                return Page();
            }
        }
    }
}
