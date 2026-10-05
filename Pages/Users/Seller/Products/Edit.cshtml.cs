using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.Products
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Product Product { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            // 1) Charger product
            Product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (Product == null) return NotFound();

            // 2) R?cup?rer le nom d'utilisateur hors des lambdas
            var username = User?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                // session non connect?e ou nom d'utilisateur manquant
                return Forbid(); // ou RedirectToPage("/Account/Login");
            }

            // 3) Charger l'utilisateur (une seule variable currentUser)
            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == username);

            if (currentUser == null) return Forbid();

            // 4) Charger l'entreprise du Seller en incluant CompanySectors->Category
            var company = await _context.Companies
                .Include(c => c.CompanySectors)
                    .ThenInclude(cs => cs.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SellerId == currentUser.Id);

            if (company == null) return NotFound();

            // 5) R?cup?rer le secteur principal en m?moire (s?curit? null)
            var sector = company.CompanySectors?.FirstOrDefault(cs => cs.IsPrimary) 
                        ?? company.CompanySectors?.FirstOrDefault();

            int? parentCategoryId = sector?.CategoryId;

            // 6) Construire les SelectList en prot?geant les nulls
            List<Category> categories;
            if (parentCategoryId.HasValue)
            {
                categories = await _context.Categories
                    .Where(c => c.ParentCategoryId == parentCategoryId.Value && !c.IsDeleted)
                    .ToListAsync();
            }
            else
            {
                categories = await _context.Categories
                    .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
                    .ToListAsync();
            }

            // Fallback : Si aucune cat?gorie trouv?e, prendre toutes les cat?gories actives
            if (!categories.Any())
            {
                categories = await _context.Categories
                    .Where(c => !c.IsDeleted)
                    .Take(50) // Limiter pour les performances
                    .ToListAsync();
            }

            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", Product.CategoryId);

            ViewData["CompanyId"] = new SelectList(
                _context.Companies.Where(c => c.Id == company.Id),
                "Id", "Name", Product.CompanyId);

            return Page();
        }


        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(Product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(Product.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
