using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SearchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("suggestions")]
        public async Task<ActionResult<IEnumerable<string>>> Suggestions([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return BadRequest("Requete trop courte");
            }

            return await _context.Products
                .AsNoTracking()
                .WhereSellableProducts()
                .Where(p => p.Name.Contains(query))
                .Select(p => p.Name)
                .Distinct()
                .Take(5)
                .ToListAsync();
        }

        [HttpGet("products")]
        public async Task<IActionResult> Products(
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 48);

            var query = _context.Products
                .AsNoTracking()
                .WhereSellableProducts();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var pattern = $"%{term}%";
                query = query.Where(p => EF.Functions.ILike(p.Name, pattern)
                    || (p.Description != null && EF.Functions.ILike(p.Description, pattern))
                    || EF.Functions.ILike(p.Category.Name, pattern)
                    || EF.Functions.ILike(p.Company.Name, pattern));
            }

            if (categoryId.HasValue)
            {
                var selectedCategoryId = categoryId.Value;
                var isParentCategory = await _context.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == selectedCategoryId && c.ParentCategoryId == null);

                query = isParentCategory
                    ? query.Where(p => p.Category.ParentCategoryId == selectedCategoryId)
                    : query.Where(p => p.CategoryId == selectedCategoryId);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var products = await query
                .OrderByDescending(p => p.IsPromoted)
                .ThenByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    CompanyId = p.CompanyId,
                    Price = p.Variants
                        .Where(v => !v.IsDeleted
                            && v.IsAvailable
                            && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                || (v.Stock == null && v.Quantity > 0)))
                        .OrderBy(v => v.Price)
                        .Select(v => v.Price)
                        .FirstOrDefault(),
                    Stock = p.Variants
                        .Where(v => !v.IsDeleted
                            && v.IsAvailable
                            && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                || (v.Stock == null && v.Quantity > 0)))
                        .Sum(v => v.Stock != null && !v.Stock.IsDeleted ? v.Stock.Quantity : v.Quantity),
                    ImageUrl = p.Variants
                        .Where(v => !v.IsDeleted
                            && v.IsAvailable
                            && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                || (v.Stock == null && v.Quantity > 0)))
                        .OrderBy(v => v.Price)
                        .SelectMany(v => v.Images)
                        .Select(i => i.Url)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new
            {
                products,
                totalItems,
                totalPages,
                currentPage = page,
                pageSize
            });
        }
    }
}
