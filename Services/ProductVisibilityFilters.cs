using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{
    public static class ProductVisibilityFilters
    {
        public static IQueryable<Product> WhereSellableProducts(this IQueryable<Product> query)
        {
            return query.Where(p => !p.IsDeleted
                && p.Category != null
                && !p.Category.IsDeleted
                && p.Category.IsActive
                && p.Category.Type == CategoryType.Product
                && p.Category.ParentCategoryId != null
                && p.Category.ParentCategory != null
                && !p.Category.ParentCategory.IsDeleted
                && p.Category.ParentCategory.IsActive
                && p.Variants.Any(v => !v.IsDeleted
                    && v.IsAvailable
                    && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                        || (v.Stock == null && v.Quantity > 0))));
        }

        public static IQueryable<ProductVariant> WhereSellableVariants(this IQueryable<ProductVariant> query)
        {
            return query.Where(v => !v.IsDeleted
                && v.IsAvailable
                && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                    || (v.Stock == null && v.Quantity > 0))
                && v.Product != null
                && !v.Product.IsDeleted
                && v.Product.Category != null
                && !v.Product.Category.IsDeleted
                && v.Product.Category.IsActive
                && v.Product.Category.Type == CategoryType.Product
                && v.Product.Category.ParentCategoryId != null
                && v.Product.Category.ParentCategory != null
                && !v.Product.Category.ParentCategory.IsDeleted
                && v.Product.Category.ParentCategory.IsActive);
        }

        public static bool IsSellableVariant(ProductVariant variant)
        {
            if (variant == null || variant.IsDeleted || !variant.IsAvailable)
            {
                return false;
            }

            var stockQuantity = variant.Stock != null && !variant.Stock.IsDeleted
                ? variant.Stock.Quantity
                : variant.Quantity;

            return stockQuantity > 0;
        }
    }
}
