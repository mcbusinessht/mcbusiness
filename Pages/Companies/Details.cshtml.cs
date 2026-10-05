using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Companies
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Company Company { get; set; }
        public List<Category> SubCategories { get; set; } = new List<Category>();
        public List<Product> FeaturedProducts { get; set; } = new List<Product>();

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Support des deux formats: /Details/2 et /Details?id=2
            if (!Id.HasValue && int.TryParse(Request.Query["id"], out int queryId))
            {
                Id = queryId;
            }

            if (!Id.HasValue) return NotFound();

            Company = await _context.Companies
                .Include(c => c.CompanySectors)
                    .ThenInclude(cs => cs.Category)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.City)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Country)
                .Include(c => c.Seller)
                .FirstOrDefaultAsync(m => m.Id == Id.Value);

            if (Company == null) return NotFound();

            // Récupération des IDs des secteurs de la compagnie
            var companySectorIds = Company.CompanySectors
                .Where(cs => cs.Category != null)
                .Select(cs => cs.Category.Id)
                .ToList();

            // Récupération des sous-catégories basées sur les secteurs de la compagnie
            if (companySectorIds.Any())
            {
                SubCategories = await _context.Categories
                    .Where(c => companySectorIds.Contains(c.ParentCategoryId ?? 0) || companySectorIds.Contains(c.Id))
                    .Include(c => c.Products.Where(p => p.CompanyId == Id.Value && !p.IsDeleted))
                    .Where(c => c.Products.Any(p => p.CompanyId == Id.Value && !p.IsDeleted))
                    .ToListAsync();
            }

            // Récupération des produits
            FeaturedProducts = await _context.Products
                .Where(p => p.CompanyId == Id.Value && !p.IsDeleted)
                .Include(p => p.Category)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
                .ToListAsync();

            return Page();
        }

        // Méthode utilitaire pour obtenir le secteur principal
        public Category GetPrimarySector()
        {
            return Company.CompanySectors?
                .FirstOrDefault(cs => cs.IsPrimary)?.Category
                ?? Company.CompanySectors?.FirstOrDefault()?.Category;
        }

        // Méthode utilitaire pour obtenir tous les secteurs
        public List<Category> GetAllSectors()
        {
            return Company.CompanySectors?
                .Where(cs => cs.Category != null)
                .Select(cs => cs.Category)
                .ToList() ?? new List<Category>();
        }
    }
}
