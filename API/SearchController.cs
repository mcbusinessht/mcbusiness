using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.API
{
    [Route("api/legacy-search")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SearchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("suggestions")]
        public async Task<ActionResult<IEnumerable<string>>> GetSuggestions([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return BadRequest("Query too short");
            }

            return await _context.Products
                .Where(p => p.Name.Contains(query))
                .Select(p => p.Name)
                .Distinct()
                .Take(5)
                .ToListAsync();
        }

        /// <summary>
        /// API pour rechercher des produits avec pagination - utilisée par le générateur de liens ambassadeur
        /// </summary>
        [HttpGet("products")]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string search = "",
            [FromQuery] int? categoryId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            try
            {
                // Valider les paramètres
                page = Math.Max(1, page);
                pageSize = Math.Min(50, Math.Max(1, pageSize)); // Limiter à 50 max

                // Construire la requête de base
                var query = _context.Products
                    .Where(p => !p.IsDeleted)
                    .Include(p => p.Variants)
                        .ThenInclude(v => v.Images)
                    .AsQueryable();

                // Filtrer par recherche textuelle
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(p => 
                        p.Name.ToLower().Contains(search.ToLower()) ||
                        p.Description.ToLower().Contains(search.ToLower())
                    );
                }

                // Filtrer par catégorie
                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                // Compter le total
                var totalProducts = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalProducts / pageSize);

                // Appliquer la pagination
                var products = await query
                    .OrderBy(p => p.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        description = p.Description,
                        price = p.Variants.Any() ? p.Variants.Min(v => v.Price) : 0,
                        imageUrl = p.Variants.Any() && p.Variants.Any(v => v.Images.Any()) 
                                  ? p.Variants.First(v => v.Images.Any()).Images.First().Url 
                                  : null,
                        categoryId = p.CategoryId
                    })
                    .ToListAsync();

                return Ok(new
                {
                    products = products,
                    totalCount = totalProducts,
                    totalPages = totalPages,
                    currentPage = page,
                    pageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erreur lors de la recherche de produits" });
            }
        }
    }
}
