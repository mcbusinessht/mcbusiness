using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;


namespace MCBusiness.Pages.Shop.Categories
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

        [BindProperty(SupportsGet = true)]
        public int? CategoryId { get; set; }

        [BindProperty]
        public List<ProductVariant> PV {get; private set;} = new List<ProductVariant>();
        public List<Category> Categories {get; private set; } 
        public ProductVariant variant { get; set; }
        public Category ParentCategory {get; private set;}
        [BindProperty]
        public List<ProductImage> Images {get;  private set; } = new List<ProductImage>();
        public ProductImage img { get; set; }
    


        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public async Task <IActionResult> OnGetAsync()
        {
            // Support des deux formats: /Shop/Categories/{id} et /Shop/Categories?id={id}
            if (!Id.HasValue && int.TryParse(Request.Query["id"], out int queryId))
            {
                Id = queryId;
            }

            if (!Id.HasValue) return Redirect("/Shop");

            ParentCategory = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == Id.Value && !c.IsDeleted && c.IsActive);
            if (ParentCategory == null) return Redirect("/Shop");

            // Charger les sous-catégories directes avec leurs produits visibles.
            Categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.ParentCategoryId == ParentCategory.Id
                    && !c.IsDeleted
                    && c.IsActive
                    && c.Type == CategoryType.Product)
                .Include(c => c.Products.Where(p => !p.IsDeleted
                    && p.Variants.Any(v => v.IsAvailable
                        && !v.IsDeleted
                        && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                            || (v.Stock == null && v.Quantity > 0)))))
                    .ThenInclude(p => p.Variants.Where(v => v.IsAvailable
                        && !v.IsDeleted
                        && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                            || (v.Stock == null && v.Quantity > 0))))
                        .ThenInclude(v => v.Currency)
                .OrderBy(c => c.Name)
                .AsSplitQuery()
                .ToListAsync();

            Categories = Categories
                .Where(c => c.Products?.Any() == true)
                .ToList();

            var variantIds = Categories
                .SelectMany(c => c.Products ?? Enumerable.Empty<Product>())
                .SelectMany(p => p.Variants ?? Enumerable.Empty<ProductVariant>())
                .Select(v => v.Id)
                .ToList();

            Images = await _context.ProductImages
                .AsNoTracking()
                .Where(img => variantIds.Contains(img.ProductVariantId))
                .ToListAsync();

            PV = Categories
                .SelectMany(c => c.Products ?? Enumerable.Empty<Product>())
                .SelectMany(p => p.Variants ?? Enumerable.Empty<ProductVariant>())
                .ToList();

            return Page();
        }

    }
}

