using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MCBusiness.Services;

namespace MCBusiness.Pages.Shop.Categories.Products
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public Category Category { get; private set; }
        public Dictionary<int, ProductImage> VariantImages { get; private set; }
        public List<Product> Products { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Support des deux formats: /Shop/Categories/Products/{id} et /Shop/Categories/Products?id={id}
            if (!Id.HasValue && int.TryParse(Request.Query["id"], out int queryId))
            {
                Id = queryId;
            }

            if (!Id.HasValue) return Redirect("/Shop");

            Category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == Id.Value
                    && !c.IsDeleted
                    && c.IsActive
                    && c.Type == CategoryType.Product
                    && c.ParentCategoryId != null);
            if (Category == null) return Redirect("/Shop");

            // Chargement des produits de la catégorie
            Products = await _context.Products
                .AsNoTracking()
                .WhereSellableProducts()
                .Where(p => p.CategoryId == Category.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Récupération des IDs des produits pour les jointures
            var productIds = Products.Select(p => p.Id).ToList();

            // Chargement des variantes disponibles avec leurs relations
            var variants = await _context.ProductVariants
                .AsNoTracking()
                .WhereSellableVariants()
                .Where(v => productIds.Contains(v.ProductId))
                .Include(v => v.Currency)
                .Include(v => v.Stock)
                .Include(v => v.ProductVariantCharacteristics)
                    .ThenInclude(pvc => pvc.CharacteristicValue)
                    .ThenInclude(cv => cv.Characteristic)
                .ToListAsync();

            // Chargement des images pour les variantes existantes
            var variantIds = variants.Select(v => v.Id).ToList();
            var images = await _context.ProductImages
                .AsNoTracking()
                .Where(i => variantIds.Contains(i.ProductVariantId))
                .ToListAsync();

            // Création d'un dictionnaire d'images par variante
            VariantImages = images
                .GroupBy(i => i.ProductVariantId)
                .ToDictionary(g => g.Key, g => g.First());

            // Assignation des variantes aux produits
            foreach (var product in Products)
            {
                product.Variants = variants
                    .Where(v => v.ProductId == product.Id)
                    .ToList();
            }

            return Page();
        }
    }
}

