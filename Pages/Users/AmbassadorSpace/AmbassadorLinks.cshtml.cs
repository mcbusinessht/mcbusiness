using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.AmbassadorSpace
{
    [Authorize(Policy = "AmbassadorPolicy")]
    public class AmbassadorLinksModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAmbassadorAccessService _ambassadorAccess;
        private readonly ILogger<AmbassadorLinksModel> _logger;

        public AmbassadorLinksModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IAmbassadorAccessService ambassadorAccess,
            ILogger<AmbassadorLinksModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _ambassadorAccess = ambassadorAccess;
            _logger = logger;
        }

        public ApplicationUser CurrentUser { get; set; }
        public List<Category> Categories { get; set; } = new();
        public bool HasActiveSubscription { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                CurrentUser = await _userManager.GetUserAsync(User);
                if (CurrentUser == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                // Vérifier l'accès ambassadeur
                HasActiveSubscription = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(CurrentUser.Id);
                
                if (!HasActiveSubscription)
                {
                    TempData["ErrorMessage"] = "Vous n'avez pas d'abonnement ambassadeur actif.";
                    return RedirectToPage("/Plans/Index");
                }

                // Charger les catégories pour le filtre
                Categories = await _context.Categories
                    .Where(c => !c.IsDeleted
                        && c.IsActive
                        && c.Type == CategoryType.Product
                        && c.ParentCategoryId != null
                        && c.Products.Any(p => !p.IsDeleted
                            && p.Variants.Any(v => !v.IsDeleted
                                && v.IsAvailable
                                && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                    || (v.Stock == null && v.Quantity > 0)))))
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ambassador links page for user {UserId}", CurrentUser?.Id);
                TempData["ErrorMessage"] = "Erreur lors du chargement de la page.";
                return RedirectToPage("/Users/AmbassadorSpace/Dashboard");
            }
        }
    }
}
