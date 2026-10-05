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
    public class AmbassadorNetworkModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly IAmbassadorAccessService _ambassadorAccess;
        private readonly ILogger<AmbassadorNetworkModel> _logger;

        public AmbassadorNetworkModel(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            IAmbassadorAccessService ambassadorAccess,
            ILogger<AmbassadorNetworkModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _ambassadorAccess = ambassadorAccess;
            _logger = logger;
        }

        // Propriétés pour les données du réseau
        public ApplicationUser CurrentUser { get; set; }
        public int Generation1Count { get; set; }
        public int Generation2Count { get; set; }
        public int Generation3Count { get; set; }
        public List<ReferralNetworkUser> Generation1Users { get; set; } = new();
        public List<ReferralNetworkUser> Generation2Users { get; set; } = new();
        public List<ReferralNetworkUser> Generation3Users { get; set; } = new();

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
                var hasAccess = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(CurrentUser.Id);
                if (!hasAccess)
                {
                    TempData["ErrorMessage"] = "Accès refusé. Vous devez être ambassadeur actif.";
                    return RedirectToPage("/Plans/Index");
                }

                // Charger les données du réseau
                await LoadNetworkDataAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du réseau ambassadeur pour {UserId}", CurrentUser?.Id);
                TempData["ErrorMessage"] = "Erreur lors du chargement du réseau.";
                return Page();
            }
        }

        private async Task LoadNetworkDataAsync()
        {
            // COOKIES-BASED: Seulement génération 1 (clients directs via mon lien)
            // Les générations 2 et 3 n'existent pas dans le système ambassadeur cookies
            
            // Récupérer tous les utilisateurs uniques qui ont commandé via mon code
            var ordersWithMyCode = await _context.Orders
                .Where(o => o.ReferredByAmbassador == CurrentUser.Id)
                .Include(o => o.User)
                .ToListAsync();

            var uniqueUserIds = ordersWithMyCode
                .Select(o => o.UserId)
                .Distinct()
                .ToList();

            var uniqueUsers = await _context.Users
                .Where(u => uniqueUserIds.Contains(u.Id))
                .ToListAsync();

            Generation1Users = await BuildReferralNetworkUsersAsync(uniqueUsers);
            Generation1Count = Generation1Users.Count;

            // Générations 2 et 3 sont vides dans le système ambassadeur (cookies only)
            Generation2Users = new List<ReferralNetworkUser>();
            Generation2Count = 0;
            Generation3Users = new List<ReferralNetworkUser>();
            Generation3Count = 0;
        }

        private async Task<List<ReferralNetworkUser>> BuildReferralNetworkUsersAsync(List<ApplicationUser> users)
        {
            var networkUsers = new List<ReferralNetworkUser>();

            foreach (var user in users)
            {
                // Vérifier si l'utilisateur est ambassadeur
                var isAmbassador = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(user.Id);

                // Compter les commandes de l'utilisateur
                var orderCount = await _context.Orders.CountAsync(o => o.UserId == user.Id);

                networkUsers.Add(new ReferralNetworkUser
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    JoinDate = user.RegistrationDate.DateTime,
                    IsAmbassador = isAmbassador,
                    OrderCount = orderCount,
                    PhoneNumber = user.PhoneNumber
                });
            }

            return networkUsers.OrderByDescending(u => u.JoinDate).ToList();
        }
    }

    public class ReferralNetworkUser
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime JoinDate { get; set; }
        public bool IsAmbassador { get; set; }
        public int OrderCount { get; set; }
        public string PhoneNumber { get; set; }
        
        public string DisplayName => !string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName) 
            ? $"{FirstName} {LastName}" 
            : Username;
    }
}
