using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Services
{
    public interface IOptimizedProductService
    {
        Task<List<Product>> GetHomePageProductsAsync();
        Task<List<Category>> GetShopCategoriesAsync();
        Task<List<ProductVariant>> GetProductVariantsByCategoryAsync(int categoryId, int page = 1, int pageSize = 20);
        Task<Product> GetProductWithDetailsAsync(int productId);
    }

    public class OptimizedProductService : IOptimizedProductService
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;
        private readonly ILogger<OptimizedProductService> _logger;

        public OptimizedProductService(AppDbContext context, ICacheService cache, ILogger<OptimizedProductService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<Product>> GetHomePageProductsAsync()
        {
            const string cacheKey = "homepage_products_sellable_v2";
            
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                // OPTIMISÉ: Projection SELECT au lieu d'Include profonds
                var products = await _context.Products
                    .AsNoTracking()
                    .WhereSellableProducts()
                    .Select(p => new Product
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        CategoryId = p.CategoryId,
                        CompanyId = p.CompanyId,
                        IsPromoted = p.IsPromoted,
                        ProductView = p.ProductView,
                        Reviews = p.Reviews.Where(r => !r.IsDeleted).ToList(),
                        Variants = p.Variants
                            .Where(v => v.IsAvailable
                                && !v.IsDeleted
                                && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                    || (v.Stock == null && v.Quantity > 0)))
                            .OrderBy(v => v.Price)
                            .Take(1)
                            .Select(v => new ProductVariant
                            {
                                Id = v.Id,
                                Sku = v.Sku,
                                Price = v.Price,
                                Quantity = v.Quantity,
                                ProductId = v.ProductId,
                                CurrencyBase = v.CurrencyBase,
                                Currency = v.Currency,
                                Images = v.Images.Take(1).ToList()
                            }).ToList()
                    })
                    .Take(24)
                    .ToListAsync();

                // Mélanger en mémoire pour éviter les requêtes complexes
                return products.OrderBy(p => Guid.NewGuid()).ToList();
                
            }, TimeSpan.FromMinutes(15)); // Cache plus court pour la page d'accueil
        }

        public async Task<List<Category>> GetShopCategoriesAsync()
        {
            const string cacheKey = "shop_categories_sellable_v2";
            
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                // OPTIMISÉ: Une seule requête au lieu de N+1 (foreach avec requête DB)
                var mainCategories = await _context.Categories
                    .AsNoTracking()
                    .Where(c => c.ParentCategoryId == null && !c.IsDeleted && c.IsActive)
                    .Include(c => c.SubCategories.Where(sc => 
                        !sc.IsDeleted
                        && sc.IsActive
                        && sc.Type == CategoryType.Product
                        && sc.Products.Any(p => !p.IsDeleted
                            && p.Variants.Any(pv => pv.IsAvailable
                                && !pv.IsDeleted
                                && ((pv.Stock != null && !pv.Stock.IsDeleted && pv.Stock.Quantity > 0)
                                    || (pv.Stock == null && pv.Quantity > 0))))))
                        .ThenInclude(sc => sc.Products.Where(p => !p.IsDeleted && 
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
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                // Filtrer et mélanger en mémoire
                var result = mainCategories
                    .Where(c => c.SubCategories != null && c.SubCategories.Any(sc => sc.Products.Any()))
                    .Select(c => {
                        // Mélanger les sous-catégories
                        c.SubCategories = c.SubCategories
                            .Where(sc => sc.Products.Any())
                            .OrderBy(sc => Guid.NewGuid())
                            .ToList();
                        return c;
                    })
                    .OrderBy(c => Guid.NewGuid())
                    .ToList();

                return result;
                
            }, TimeSpan.FromHours(1)); // Cache plus long pour les catégories
        }

        public async Task<List<ProductVariant>> GetProductVariantsByCategoryAsync(int categoryId, int page = 1, int pageSize = 20)
        {
            var cacheKey = $"category_products_sellable_v2_{categoryId}_page_{page}_size_{pageSize}";
            
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var skip = (page - 1) * pageSize;
                
                // OPTIMISÉ: Projection SELECT + AsNoTracking
                return await _context.ProductVariants
                    .AsNoTracking()
                    .WhereSellableVariants()
                    .Where(pv => pv.Product.CategoryId == categoryId)
                    .Select(pv => new ProductVariant
                    {
                        Id = pv.Id,
                        Sku = pv.Sku,
                        Price = pv.Price,
                        Quantity = pv.Quantity,
                        ProductId = pv.ProductId,
                        CurrencyBase = pv.CurrencyBase,
                        IsAvailable = pv.IsAvailable,
                        Product = new Product
                        {
                            Id = pv.Product.Id,
                            Name = pv.Product.Name,
                            Description = pv.Product.Description,
                            CategoryId = pv.Product.CategoryId,
                            CompanyId = pv.Product.CompanyId
                        },
                        Currency = pv.Currency,
                        Images = pv.Images.Take(1).ToList()
                    })
                    .OrderBy(pv => pv.Product.Name)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();
                    
            }, TimeSpan.FromMinutes(30));
        }

        public async Task<Product> GetProductWithDetailsAsync(int productId)
        {
            var cacheKey = $"product_details_sellable_v2_{productId}";
            
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                // OPTIMISÉ: Projection SELECT au lieu de 10+ niveaux d'Include
                return await _context.Products
                    .AsNoTracking()
                    .WhereSellableProducts()
                    .Where(p => p.Id == productId)
                    .Select(p => new Product
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        CategoryId = p.CategoryId,
                        BrandId = p.BrandId,
                        CompanyId = p.CompanyId,
                        IsPromoted = p.IsPromoted,
                        ProductView = p.ProductView,
                        CreatedAt = p.CreatedAt,
                        // Relations nécessaires
                        Category = p.Category != null ? new Category 
                        { 
                            Id = p.Category.Id, 
                            Name = p.Category.Name 
                        } : null,
                        Brand = p.Brand != null ? new Brand 
                        { 
                            Id = p.Brand.Id, 
                            Name = p.Brand.Name 
                        } : null,
                        // Variantes avec leurs détails
                        Variants = p.Variants
                            .Where(v => v.IsAvailable
                                && !v.IsDeleted
                                && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                    || (v.Stock == null && v.Quantity > 0)))
                            .Select(v => new ProductVariant
                            {
                                Id = v.Id,
                                Sku = v.Sku,
                                Price = v.Price,
                                Quantity = v.Quantity,
                                ProductId = v.ProductId,
                                CurrencyBase = v.CurrencyBase,
                                IsAvailable = v.IsAvailable,
                                Currency = v.Currency,
                                Images = v.Images.Select(img => new ProductImage
                                {
                                    Id = img.Id,
                                    Url = img.Url,
                                    ProductVariantId = img.ProductVariantId
                                }).ToList(),
                                ProductVariantCharacteristics = v.ProductVariantCharacteristics
                                    .Select(pvc => new ProductVariantCharacteristic
                                    {
                                        ProductVariantId = pvc.ProductVariantId,
                                        CharacteristicId = pvc.CharacteristicId,
                                        CharacteristicValueId = pvc.CharacteristicValueId,
                                        CharacteristicValue = new CharacteristicValue
                                        {
                                            Id = pvc.CharacteristicValue.Id,
                                            Value = pvc.CharacteristicValue.Value,
                                            CharacteristicId = pvc.CharacteristicValue.CharacteristicId,
                                            Characteristic = new Characteristic
                                            {
                                                Id = pvc.CharacteristicValue.Characteristic.Id,
                                                Name = pvc.CharacteristicValue.Characteristic.Name
                                            }
                                        }
                                    }).ToList()
                            }).ToList(),
                        // Avis limités
                        Reviews = p.Reviews
                            .Where(r => !r.IsDeleted)
                            .OrderByDescending(r => r.DateReview)
                            .Take(10)
                            .Select(r => new ProductReview
                            {
                                Id = r.Id,
                                Rate = r.Rate,
                                Comment = r.Comment,
                                DateReview = r.DateReview,
                                UserId = r.UserId,
                                User = new ApplicationUser
                                {
                                    Id = r.User.Id,
                                    UserName = r.User.UserName,
                                    FirstName = r.User.FirstName
                                }
                            }).ToList()
                    })
                    .FirstOrDefaultAsync();
                    
            }, TimeSpan.FromMinutes(30));
        }
    }
}
