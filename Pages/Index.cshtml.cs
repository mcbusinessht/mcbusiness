using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using MCBusiness.Interfaces;
using MCBusiness.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;

namespace MCBusiness.Pages
{

    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IOptimizedProductService _productService;
        private readonly IReCaptchaService _reCaptchaService;
        private readonly ReCaptchaSettings _reCaptchaSettings;

        public IndexModel(ILogger<IndexModel> logger , AppDbContext context, IEmailSender emailSender, 
            IOptimizedProductService productService, IReCaptchaService reCaptchaService, 
            IOptions<ReCaptchaSettings> reCaptchaSettings)
        {
            _logger = logger;
            _context=context;
            _emailSender=emailSender;
            _productService=productService;
            _reCaptchaService = reCaptchaService;
            _reCaptchaSettings = reCaptchaSettings.Value;
        }
        [BindProperty]
        public List<AmbassadorPlan> plans{get; private set;}= new List<AmbassadorPlan>();
        [BindProperty] 
        public ApplicationUser CurrentUser{get; private set;}=null;
        public SystemMedia ImagePath{get;private set;}=null;
        public Newsletter newsletter{get; set;}
        [BindProperty, MaxLength(100)]
        public string email {get; set;}

        public List<Product > ProductList {get; set; }
        public List<Category> FeaturedCategories { get; set; } = new List<Category>();
        public List<Company> FeaturedCompanies { get; set; } = new List<Company>();
        public Dictionary<int, int> FeaturedCompanyProductCounts { get; set; } = new Dictionary<int, int>();
        public string ReCaptchaSiteKey => _reCaptchaSettings.SiteKey;

        public async Task<IActionResult> OnGetAsync()
        {
            // Exécution séquentielle pour éviter les problèmes de DbContext
            plans = await _context.AmbassadorPlans.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();
            
            if (User.Identity.IsAuthenticated)
            {
                CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            }
            
            ImagePath = await _context.SystemMedias.FirstOrDefaultAsync(m => m.Name == "ImageIndex1");
            
            // Le service de produits utilise son propre context injecté
            ProductList = await _productService.GetHomePageProductsAsync();
            FeaturedCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                    && c.IsActive
                    && c.Type == CategoryType.Product
                    && c.ParentCategoryId == null
                    && c.SubCategories.Any(sc => !sc.IsDeleted
                            && sc.IsActive
                            && sc.Type == CategoryType.Product
                            && sc.Products.Any(p => !p.IsDeleted
                                && p.Variants.Any(v => v.IsAvailable
                                    && !v.IsDeleted
                                    && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                        || (v.Stock == null && v.Quantity > 0))))))
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted
                    && sc.IsActive
                    && sc.Type == CategoryType.Product
                    && sc.Products.Any(p => !p.IsDeleted
                        && p.Variants.Any(v => v.IsAvailable
                            && !v.IsDeleted
                            && ((v.Stock != null && !v.Stock.IsDeleted && v.Stock.Quantity > 0)
                                || (v.Stock == null && v.Quantity > 0))))))
                .OrderBy(c => c.Name)
                .Take(8)
                .AsSplitQuery()
                .ToListAsync();

            FeaturedCompanies = await _context.Companies
                .AsNoTracking()
                .Include(c => c.CompanySectors)
                    .ThenInclude(cs => cs.Category)
                .Where(c => !c.IsDeleted
                    && !c.IsBlocked
                    && _context.Products.WhereSellableProducts().Any(p => p.CompanyId == c.Id))
                .OrderByDescending(c => c.IsVerified)
                .ThenBy(c => c.Name)
                .Take(6)
                .ToListAsync();

            var featuredCompanyIds = FeaturedCompanies.Select(c => c.Id).ToList();
            FeaturedCompanyProductCounts = await _context.Products
                .AsNoTracking()
                .WhereSellableProducts()
                .Where(p => featuredCompanyIds.Contains(p.CompanyId)
                    && !p.IsDeleted)
                .GroupBy(p => p.CompanyId)
                .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var normalizedEmail = email?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    ModelState.AddModelError(nameof(email), "Veuillez entrer une adresse email valide.");
                    await OnGetAsync();
                    return Page();
                }

                var existingSubscription = await _context.Newsletters
                    .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail);
                if (existingSubscription != null)
                {
                    TempData["InfoMessage"] = "Cette adresse email est deja inscrite a la newsletter.";
                    return RedirectToPage("./Index");
                }

                newsletter = new Newsletter
                {
                    Email = normalizedEmail
                };

                _context.Newsletters.Add(newsletter);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Felicitations, vous etes abonne avec succes a notre newsletter.";
                await _emailSender.SendEmailAsync(
                    normalizedEmail,
                    "MCBusiness Newsletter",
                    "Felicitations, vous etes abonne avec succes a la newsletter. Vous recevrez les dernieres actualites, offres et conseils directement dans votre boite mail.");

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'abonnement a la newsletter");
                ModelState.AddModelError(string.Empty, "Une erreur est survenue. Veuillez reessayer.");
                await OnGetAsync();
                return Page();
            }
        }
    }
}
