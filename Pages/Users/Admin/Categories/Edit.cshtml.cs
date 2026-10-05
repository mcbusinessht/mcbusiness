using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Categories
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        // Propriétés bindées individuellement au lieu de l'objet complet
        [BindProperty]
        public int Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public CategoryType Type { get; set; }

        [BindProperty]
        public int? ParentCategoryId { get; set; }

        [BindProperty]
        public string? UrlLogo { get; set; }

        [BindProperty]
        public bool IsDeleted { get; set; }

        // DateAdded remplacé par CreatedAt dans BaseEntity

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FirstOrDefaultAsync(m => m.Id == id);
            if (category == null) return NotFound();

            // Initialiser les propriétés avec les valeurs de la catégorie
            Id = category.Id;
            Name = category.Name;
            Type = category.Type;
            ParentCategoryId = category.ParentCategoryId;
            UrlLogo = category.UrlLogo;
            IsDeleted = category.IsDeleted;
            // DateAdded géré par BaseEntity

            ViewData["ParentCategoryId"] = new SelectList(_context.Categories.Where(c => c.Id != id), "Id", "Name");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["ParentCategoryId"] = new SelectList(_context.Categories.Where(c => c.Id != Id), "Id", "Name");
                return Page();
            }

            // Récupérer la catégorie existante
            var existingCategory = await _context.Categories.FindAsync(Id);
            if (existingCategory == null)
            {
                return NotFound();
            }

            // Mettre à jour les propriétés
            existingCategory.Name = Name;
            existingCategory.Type = Type;
            existingCategory.ParentCategoryId = ParentCategoryId;
            existingCategory.UrlLogo = UrlLogo;
            existingCategory.IsDeleted = IsDeleted;
            // Ne pas modifier DateAdded pour préserver la date originale

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(Id)) return NotFound();
                throw;
            }
        }

        private bool CategoryExists(int id) => _context.Categories.Any(e => e.Id == id);
    }
}
