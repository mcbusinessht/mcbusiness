using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.Seller.Products
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public CreateModel(AppDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        public async Task<IActionResult> OnGet()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var company = await _context.Companies
    .Include(c => c.CompanySectors)
        .ThenInclude(cs => cs.Category)
    .Where(c => c.SellerId == currentUser.Id)
    .Select(c => new
    {
        c.Id,
        c.Name,
        c.Description,
        Categories = c.CompanySectors.Select(cs => cs.Category)
    })
    .FirstOrDefaultAsync();
            // if (company != null)
            // {
            //     company.CompanySectors = company.CompanySectors
            //         .Where(cs => cs.Category.ParentCategoryId != null)
            //         .ToList();
            // }
            if (company == null)
            {
                TempData["Error"] = "Aucune compagnie trouvée pour cet utilisateur.";
                return RedirectToPage("/Users/Seller/Dashboard");
            }

            // Récupérer les IDs des secteurs de la compagnie
            var companySectorIds = company.Categories
                .Where(c => c != null)
                .Select(c => c.Id)
                .ToList();

            // Récupérer les catégories basées sur les secteurs de la compagnie
            var categories = await _context.Categories
                .Where(c => c.ParentCategoryId != null && companySectorIds.Contains(c.ParentCategoryId.Value))
                .Where(c => !c.IsDeleted) // Utiliser IsDeleted au lieu de IsActive
                .ToListAsync();

            // Fallback : Si aucune catégorie trouvée, prendre toutes les catégories actives
            if (!categories.Any())
            {
                categories = await _context.Categories
                    .Where(c => !c.IsDeleted)
                    .Take(50) // Limiter pour les performances
                    .ToListAsync();
            }

            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name");
            ViewData["CompanyId"] = new SelectList(
                new[]
                {
                    new
                    {
                        company.Id,
                        company.Name
                    }
                },
                "Id",
                "Name"
            );
            // Stocker l'ID de la compagnie pour l'utiliser dans le formulaire
            ViewData["CurrentCompanyId"] = company.Id;

            return Page();
        }

        [BindProperty]
        public Product Product { get; set; } = default!;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Recharger les données en cas d'erreur
                await ReloadViewData();
                return Page();
            }

            // S'assurer que le produit est associé à la bonne compagnie
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.SellerId == currentUser.Id);
                
                if (company != null)
                {
                    Product.CompanyId = company.Id;
                }
            }

            _context.Products.Add(Product);
            await _context.SaveChangesAsync();

            return RedirectToPage("./ProductVariants/Create", new { id = Product.Id });
        }

        private async Task ReloadViewData()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return;

            var company = await _context.Companies
                .Include(c => c.CompanySectors)
                    .ThenInclude(cs => cs.Category)
                .FirstOrDefaultAsync(c => c.SellerId == currentUser.Id);

            if (company != null)
            {
                var companySectorIds = company.CompanySectors
                    .Where(cs => cs.Category != null)
                    .Select(cs => cs.Category.Id)
                    .ToList();

                var categories = await _context.Categories
                    .Where(c => c.ParentCategoryId != null && companySectorIds.Contains(c.ParentCategoryId.Value))
                    .Where(c => !c.IsDeleted) // Utiliser IsDeleted au lieu de IsActive
                    .ToListAsync();

                // Fallback : Si aucune catégorie trouvée, prendre toutes les catégories actives
                if (!categories.Any())
                {
                    categories = await _context.Categories
                        .Where(c => !c.IsDeleted)
                        .Take(50) // Limiter pour les performances
                        .ToListAsync();
                }

                ViewData["CategoryId"] = new SelectList(categories, "Id", "Name");
                ViewData["CompanyId"] = new SelectList(
                    new[] {new
    {
        company.Id,
        company.Name
    }}, "Id", "Name");
            }
        }
    }
}
