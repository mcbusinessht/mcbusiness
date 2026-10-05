using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;
using System.Security.Claims;

namespace MCBusiness.Controllers
{
    /// <summary>
    /// Contrôleur pour les fonctionnalités Ambassadeur
    /// Intégré harmonieusement dans l'architecture MCBusiness
    /// </summary>
    [Authorize(Policy = "AmbassadorPolicy")]
    [Route("api/ambassador")]
    [ApiController]
    public class AmbassadorController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAmbassadorAccessService _ambassadorAccess;
        private readonly AmbassadorCommissionService _commissionService;
        private readonly ILogger<AmbassadorController> _logger;

        public AmbassadorController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IAmbassadorAccessService ambassadorAccess,
            AmbassadorCommissionService commissionService,
            ILogger<AmbassadorController> logger)
        {
            _context = context;
            _userManager = userManager;
            _ambassadorAccess = ambassadorAccess;
            _commissionService = commissionService;
            _logger = logger;
        }

        #region GÉNÉRATION DE LIENS

        /// <summary>
        /// Génère un lien ambassadeur pour un produit spécifique
        /// </summary>
        [HttpPost("generate-link")]
        public async Task<IActionResult> GenerateProductLink([FromBody] GenerateLinkRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                // Vérifier l'accès ambassadeur
                var hasAccess = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(userId);
                if (!hasAccess)
                {
                    return Forbid("Abonnement ambassadeur inactif");
                }

                // Récupérer l'utilisateur
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || string.IsNullOrEmpty(user.ReferralCode))
                {
                    return BadRequest("Code de référence ambassadeur introuvable");
                }

                // Vérifier que le produit existe
                var product = await _context.Products
                    .WhereSellableProducts()
                    .FirstOrDefaultAsync(p => p.Id == request.ProductId);
                
                if (product == null)
                {
                    return NotFound("Produit introuvable ou indisponible");
                }

                // Construire le lien ambassadeur
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var productSlug = product.Name.ToLower()
                    .Replace(" ", "-")
                    .Replace("'", "");
                
                var ambassadorLink = $"{baseUrl}/Shop/Categories/Products/Details/{request.ProductId}?ref={user.ReferralCode}";
                
                _logger.LogInformation($"Generated ambassador link for user {userId}, product {request.ProductId}");

                return Ok(new
                {
                    Success = true,
                    Link = ambassadorLink,
                    ProductName = product.Name,
                    ReferralCode = user.ReferralCode,
                    Commission = await GetExpectedCommissionAsync(userId, product)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ambassador link");
                return StatusCode(500, "Erreur lors de la génération du lien");
            }
        }

        /// <summary>
        /// Génère des liens pour tous les produits d'une catégorie
        /// </summary>
        [HttpPost("generate-category-links")]
        public async Task<IActionResult> GenerateCategoryLinks([FromBody] GenerateCategoryLinksRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var hasAccess = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(userId);
                if (!hasAccess) return Forbid();

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return BadRequest();

                var products = await _context.Products
                    .WhereSellableProducts()
                    .Where(p => p.CategoryId == request.CategoryId)
                    .Take(50) // Limite pour éviter surcharge
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        Price = p.Variants
                            .Where(v => !v.IsDeleted
                                && v.IsAvailable
                                && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                    || (v.Stock == null && v.Quantity > 0)))
                            .Min(v => v.Price),
                        Link = $"{Request.Scheme}://{Request.Host}/Shop/Categories/Products/Details/{p.Id}?ref={user.ReferralCode}"
                    })
                    .ToListAsync();

                return Ok(new { Success = true, Products = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating category links");
                return StatusCode(500, "Erreur lors de la génération des liens");
            }
        }

        #endregion

        #region STATISTIQUES ET DASHBOARD

        /// <summary>
        /// Récupère les statistiques pour le dashboard ambassadeur
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetAmbassadorStats()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var hasAccess = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(userId);
                if (!hasAccess) return Forbid();

                var stats = await _commissionService.GetCommissionStatsAsync(userId);
                var subscription = await _ambassadorAccess.GetActiveSubscriptionAsync(userId);

                var response = new AmbassadorStatsResponse
                {
                    ThisMonthCommissions = stats.ThisMonthCommissions,
                    TotalCommissions = stats.TotalCommissions,
                    PendingCommissions = stats.PendingCommissions,
                    TotalSaleCommissions = stats.TotalSaleCommissions,
                    TotalReferralCommissions = stats.TotalReferralCommissions,
                    CurrentPlan = subscription?.Plan?.Name,
                    MonthlyLimit = subscription?.Plan?.Ceiling ?? 0,
                    RemainingLimit = Math.Max(0, (subscription?.Plan?.Ceiling ?? 0) - stats.ThisMonthCommissions),
                    CommissionRate = subscription?.Plan?.AmbassadorCommissionPercentage ?? 0
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ambassador stats");
                return StatusCode(500, "Erreur lors de la récupération des statistiques");
            }
        }

        /// <summary>
        /// Récupère l'historique des commissions
        /// </summary>
        [HttpGet("commissions")]
        public async Task<IActionResult> GetCommissionHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var hasAccess = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(userId);
                if (!hasAccess) return Forbid();

                var commissions = await _context.AmbassadorCommissions
                    .Where(c => c.AmbassadorId == userId)
                    .Include(c => c.Order)
                    .Include(c => c.Company)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.Id,
                        c.Amount,
                        c.Type,
                        c.Status,
                        c.CreatedAt,
                        c.AvailableAt,
                        c.PaidAt,
                        OrderId = c.Order.Id,
                        CompanyName = c.Company.Name,
                        Generation = 1 // Generation supprimée
                    })
                    .ToListAsync();

                var total = await _context.AmbassadorCommissions
                    .CountAsync(c => c.AmbassadorId == userId);

                return Ok(new
                {
                    Success = true,
                    Commissions = commissions,
                    TotalCount = total,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)total / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving commission history");
                return StatusCode(500, "Erreur lors de la récupération de l'historique");
            }
        }

        /// <summary>
        /// Récupère les catégories de produits pour le générateur de liens
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => !c.IsDeleted
                        && c.IsActive
                        && c.Type == CategoryType.Product
                        && c.ParentCategoryId != null
                        && c.Products.Any(p => !p.IsDeleted
                            && p.Variants.Any(v => !v.IsDeleted
                                && v.IsAvailable
                                && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                    || (v.Stock == null && v.Quantity > 0)))))
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name
                    })
                    .OrderBy(c => c.name)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching categories for ambassador");
                return StatusCode(500, new { error = "Erreur lors de la récupération des catégories" });
            }
        }

        /// <summary>
        /// Récupère les entreprises partenaires disponibles
        /// </summary>
        [HttpGet("partner-companies")]
        public async Task<IActionResult> GetPartnerCompanies()
        {
            try
            {
                var companies = await _context.Companies
                    .Where(c => !c.IsDeleted)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.UrlLogo,
                        ProductCount = _context.Products
                            .WhereSellableProducts()
                            .Count(p => p.CompanyId == c.Id)
                    })
                    .Where(c => c.ProductCount > 0)
                    .ToListAsync();

                return Ok(new { Success = true, Companies = companies });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner companies");
                return StatusCode(500, "Erreur lors de la récupération des entreprises");
            }
        }

        #endregion

        #region RÉSEAU DE PARRAINAGE

        /// <summary>
        /// Récupère les informations du réseau de parrainage
        /// </summary>
        [HttpGet("referral-network")]
        public async Task<IActionResult> GetReferralNetwork()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var hasAccess = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(userId);
                if (!hasAccess) return Forbid();

                // Génération 1 (directs)
                var generation1 = await _context.Referrals
                    .Where(r => r.ReferrerId == userId)
                    .Include(r => r.Referred)
                    .Select(r => new
                    {
                        r.Referred.Id,
                        Name = $"{r.Referred.FirstName} {r.Referred.LastName}",
                        r.Referred.Email,
                        JoinDate = r.CreatedAt,
                        IsAmbassador = _context.UserRoles.Any(ur => ur.UserId == r.Referred.Id && 
                            _context.Roles.Any(role => role.Id == ur.RoleId && role.Name == "Ambassador"))
                    })
                    .ToListAsync();

                // Compteurs générations suivantes
                var generation2Count = 0;
                var generation3Count = 0;

                foreach (var gen1User in generation1)
                {
                    var gen2Count = await _context.Referrals
                        .CountAsync(r => r.ReferrerId == gen1User.Id);
                    generation2Count += gen2Count;

                    var gen2Users = await _context.Referrals
                        .Where(r => r.ReferrerId == gen1User.Id)
                        .Select(r => r.ReferredId)
                        .ToListAsync();

                    foreach (var gen2UserId in gen2Users)
                    {
                        var gen3Count = await _context.Referrals
                            .CountAsync(r => r.ReferrerId == gen2UserId);
                        generation3Count += gen3Count;
                    }
                }

                return Ok(new
                {
                    Success = true,
                    Generation1 = generation1,
                    Generation1Count = generation1.Count,
                    Generation2Count = generation2Count,
                    Generation3Count = generation3Count,
                    TotalNetwork = generation1.Count + generation2Count + generation3Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving referral network");
                return StatusCode(500, "Erreur lors de la récupération du réseau");
            }
        }

        #endregion

        #region UTILITAIRES PRIVÉS

        /// <summary>
        /// Calcule la commission attendue pour un produit
        /// </summary>
        private async Task<decimal> GetExpectedCommissionAsync(string userId, Product product)
        {
            try
            {
                var subscription = await _ambassadorAccess.GetActiveSubscriptionAsync(userId);
                if (subscription?.Plan == null) return 0;

                var minPrice = await _context.ProductVariants
                    .WhereSellableVariants()
                    .Where(pv => pv.ProductId == product.Id)
                    .OrderBy(pv => pv.Price)
                    .Select(pv => pv.Price)
                    .FirstOrDefaultAsync();
                return minPrice * subscription.Plan.AmbassadorCommissionPercentage;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }

    #region MODÈLES DE REQUÊTE/RÉPONSE

    public class GenerateLinkRequest
    {
        public int ProductId { get; set; }
    }

    public class GenerateCategoryLinksRequest
    {
        public int CategoryId { get; set; }
    }

    public class AmbassadorStatsResponse
    {
        public decimal ThisMonthCommissions { get; set; }
        public decimal TotalCommissions { get; set; }
        public decimal PendingCommissions { get; set; }
        public decimal TotalSaleCommissions { get; set; }
        public decimal TotalReferralCommissions { get; set; }
        public string? CurrentPlan { get; set; }
        public decimal MonthlyLimit { get; set; }
        public decimal RemainingLimit { get; set; }
        public decimal CommissionRate { get; set; }
    }

    #endregion
}
