using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FuzzySharp;
using MCBusiness.Services;


namespace MCBusiness.Pages
{
    public class SearchModel : PageModel
    {
        private readonly AppDbContext _context;
        private const int PageSize = 24;

        public SearchModel(AppDbContext context)
        {
            _context = context;
        }

        // Propriétés de recherche
        public string SearchQuery { get; set; }
        public List<Product> Products { get; set; } = new();
        public Dictionary<int, ProductImage> VariantImages { get; set; } = new();
        public List<Category> AvailableCategories { get; set; }
        
        // Filtres
        [BindProperty(SupportsGet = true)]
        public int? CategoryId { get; set; }
        [BindProperty(SupportsGet = true)]
        public decimal? MinPrice { get; set; }
        [BindProperty(SupportsGet = true)]
        public decimal? MaxPrice { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalResults { get; set; }
        public string DidYouMean { get; set; }

        public async Task<IActionResult> OnGetAsync(
            string query,
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            int page = 1)
        {
            if (string.IsNullOrWhiteSpace(query))
                return RedirectToPage("/Index");

            // Initialisation des paramètres
            SearchQuery = query.Trim();
            CurrentPage = Math.Max(1, page);
            CategoryId = categoryId;
            MinPrice = minPrice;
            MaxPrice = maxPrice;

            // Construction de la requête
            var queryable = _context.Products
                .WhereSellableProducts()
                .Where(p =>
                    p.Name.ToLower().Contains(SearchQuery.ToLower()) ||
                    (p.Description != null && p.Description.ToLower().Contains(SearchQuery.ToLower()))
                )
                .Include(p => p.Variants.Where(v => v.IsAvailable
                    && !v.IsDeleted
                    && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                        || (v.Stock == null && v.Quantity > 0))))
                .AsNoTracking();

            // Application des filtres
            if (CategoryId.HasValue)
                queryable = queryable.Where(p => p.CategoryId == CategoryId.Value);

            if (MinPrice.HasValue || MaxPrice.HasValue)
                queryable = queryable.Where(p => p.Variants.Any(v => 
                    (!MinPrice.HasValue || v.Price >= MinPrice.Value) && 
                    (!MaxPrice.HasValue || v.Price <= MaxPrice.Value)));

            // Calcul de la pagination
            TotalResults = await queryable.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalResults / (double)PageSize);

            // Récupération des IDs
            var productIds = await queryable
                .OrderByDescending(p => p.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .Select(p => p.Id)
                .ToListAsync();

            // Chargement des données associées
            await LoadRelatedData(productIds);

            // Suggestions si aucun résultat
            if (TotalResults == 0)
                await GenerateSearchSuggestions();

            return Page();
        }

        private async Task LoadRelatedData(List<int> productIds)
        {
            var products = await _context.Products
                .WhereSellableProducts()
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var variants = await _context.ProductVariants
                .WhereSellableVariants()
                .Where(v => productIds.Contains(v.ProductId))
                .Include(v => v.Currency)
                .ToListAsync();

            var images = await _context.ProductImages
                .Where(i => variants.Select(v => v.Id).Contains(i.ProductVariantId))
                .ToListAsync();

            VariantImages = images.GroupBy(i => i.ProductVariantId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var product in products)
                product.Variants = variants.Where(v => v.ProductId == product.Id).ToList();

            Products = products.OrderByDescending(p => p.CreatedAt).ToList();
            AvailableCategories = await _context.Categories
                .Where(c => c.ParentCategory != null
                    && !c.IsDeleted
                    && c.IsActive
                    && c.Type == CategoryType.Product)
                .ToListAsync();
        }

        private async Task GenerateSearchSuggestions()
        {
            var allProductNames = await _context.Products
                .WhereSellableProducts()
                .Select(p => p.Name)
                .ToListAsync(); 

            var suggestions = Process.ExtractTop(SearchQuery, allProductNames, cutoff: 80);
            var cutoffScore = 80; // Seuil de similarité plus élevé
            var suggestion = suggestions?
                .Where(s => s.Score >= cutoffScore)
                .FirstOrDefault()?
                .Value;

            DidYouMean = !string.IsNullOrWhiteSpace(suggestion) ? suggestion : null;
        }
    }
}
