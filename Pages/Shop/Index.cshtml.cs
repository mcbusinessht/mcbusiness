using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using MCBusiness.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;


namespace MCBusiness.Pages.Shop
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly  AppDbContext _context;
        private readonly IOptimizedProductService _productService;

        public IndexModel(ILogger<IndexModel> logger, AppDbContext context, IOptimizedProductService productService)
        {
            _logger = logger;
            _context = context;
            _productService = productService;
        }
        [BindProperty]
        public List<ProductVariant> PV {get; private set;} = new List<ProductVariant>();
        public List<Category> Categories {get; set; } = new List<Category>();
        public List<ProductImage> Images {get; set; } = new List<ProductImage>();
        
        // Propriétés de debug
        public int TotalCategories => Categories?.Count ?? 0;
        public int TotalSubCategories => Categories?.SelectMany(c => c.SubCategories ?? Enumerable.Empty<Category>()).Count() ?? 0;
        public int TotalProducts => Categories?.SelectMany(c => c.SubCategories ?? Enumerable.Empty<Category>())
            .SelectMany(sc => sc.Products ?? Enumerable.Empty<Product>()).Count() ?? 0;
        public bool HasAnyData => Categories?.Any() == true;


        public async Task<IActionResult> OnGetAsync()
        {
            try 
            {
                _logger.LogInformation("Chargement de la page Shop - Début");
                
                // Charger directement de la base de données (sans cache) pour éviter les données obsol�tes
                // sur les produits/variantes supprimés
                var mainCategories = await _context.Categories
                    .Where(c => c.ParentCategoryId == null && !c.IsDeleted && c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                Categories = new List<Category>();

                foreach (var category in mainCategories)
                {
                    var subCategories = await _context.Categories
                        .Where(c => c.ParentCategoryId == category.Id
                            && !c.IsDeleted
                            && c.IsActive
                            && c.Type == CategoryType.Product)
                        .Include(sc => sc.Products.Where(p => !p.IsDeleted && 
                                     p.Variants.Any(pv => pv.IsAvailable
                                        && !pv.IsDeleted
                                        && ((pv.Stock != null && !pv.Stock.IsDeleted && pv.Stock.Quantity > 0)
                                            || (pv.Stock == null && pv.Quantity > 0)))).Take(4))
                        .ThenInclude(p => p.Variants.Where(v => v.IsAvailable
                            && !v.IsDeleted
                            && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                || (v.Stock == null && v.Quantity > 0))).Take(1))
                        .ThenInclude(v => v.Images.Take(1))
                        .AsSplitQuery()
                        .ToListAsync();

                    // Filtrer les sous-catégories qui ont des produits valides
                    subCategories = subCategories
                        .Where(sc => sc.Products.Any())
                        .ToList();

                    if (subCategories.Any())
                    {
                        category.SubCategories = subCategories;
                        Categories.Add(category);
                    }
                }

                _logger.LogInformation("Catégories chargées: {Count}", Categories?.Count ?? 0);
                
                // Charger toutes les images nécessaires pour l'affichage
                var productIds = Categories
                    .SelectMany(c => c.SubCategories ?? Enumerable.Empty<Category>())
                    .SelectMany(sc => sc.Products ?? Enumerable.Empty<Product>())
                    .SelectMany(p => p.Variants ?? Enumerable.Empty<ProductVariant>())
                    .Select(v => v.Id)
                    .ToList();
                    
                Images = await _context.ProductImages
                    .Where(img => productIds.Contains(img.ProductVariantId))
                    .ToListAsync();
                    
                _logger.LogInformation("Images chargées: {Count} pour {ProductCount} variants", 
                    Images?.Count ?? 0, productIds.Count);

                // PV pour compatibilité (peut être supprimé si non utilisé)
                PV = new List<ProductVariant>();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement de la page Shop");
                
                // Initialiser avec des valeurs par défaut en cas d'erreur
                Categories = new List<Category>();
                Images = new List<ProductImage>();
                PV = new List<ProductVariant>();
                
                return Page();
            }
        }
    }
}
