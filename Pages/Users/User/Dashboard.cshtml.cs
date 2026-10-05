using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.User
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;
        private readonly AppDbContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly IAmbassadorAccessService _ambassadorAccessService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardModel(
            ILogger<DashboardModel> logger, 
            AppDbContext context, 
            IDashboardService dashboardService,
            IAmbassadorAccessService ambassadorAccessService,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _dashboardService = dashboardService;
            _ambassadorAccessService = ambassadorAccessService;
            _userManager = userManager;
        }

        // Données du service Dashboard
        public UserDashboardData DashboardData { get; private set; }
        public ApplicationUser CurrentUser { get; private set; }
        
        // Points de fidélité
        public int TotalPoints { get; private set; }
        
        // Vérifications d'accès (utiliser ces propriétés dans la vue)
        // NOTE: CanViewWallet retiré - le solde est visible pour TOUS les utilisateurs
        public bool CanViewCommissions { get; private set; }
        public bool CanViewAdvancedStats { get; private set; }
        public bool CanWithdraw { get; private set; }
        public string AccessDenialReason { get; private set; }
        
        // Propriétés pour compatibilité avec les vues existantes
        public Wallet CurrentWallet => DashboardData?.CurrentWallet;
        public List<Referral> Referrals => DashboardData?.Referrals ?? new();
        public List<Order> RecentOrders => DashboardData?.RecentOrders ?? new();
        public AmbassadorPlan PlanUser => DashboardData?.CurrentPlan;
        public AmbassadorSubscription ActiveSubscription => DashboardData?.ActiveSubscription;
        public Address PrimaryAddress => DashboardData?.PrimaryAddress;
        public int TotalOrders => DashboardData?.TotalOrders ?? 0;
        public int ActiveOrders => DashboardData?.ActiveOrders ?? 0;
        public int PendingPaymentOrders => DashboardData?.PendingPaymentOrders ?? 0;
        public decimal TotalGains => DashboardData?.TotalGains ?? 0;
        public string Link { get; private set; }
        public string CountdownEndDate { get; set; }
        
        // Données pour graphiques (mappage depuis le service)
        public List<string> Labels => DashboardData?.CommissionLabels ?? new();
        public List<string> LabelsGain => DashboardData?.ReferralLabels ?? new();
        public List<decimal> ValuesGain => DashboardData?.CommissionValues ?? new();
        public List<int> Values => DashboardData?.ReferralValues ?? new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToPage("/Account/Login");

            try
            {
                // Charger l'utilisateur actuel
                CurrentUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

                if (CurrentUser == null)
                {
                    ViewData["ErrorMessage"] = "Utilisateur non trouvé";
                    return Page();
                }

                // Charger toutes les données du dashboard via le service optimisé
                DashboardData = await _dashboardService.GetUserDashboardDataAsync(CurrentUser.Id);
                
                // Charger le solde des points
                TotalPoints = CurrentUser.TotalPoints;

                // ==================== VÉRIFICATIONS D'ACCÈS ====================
                // Ces vérifications décident si le contenu est affiché ou masqué
                // NOTE: CanViewWallet retiré - le solde est visible pour TOUS
                
                CanViewCommissions = await _ambassadorAccessService.CanAccessAmbassadorFeaturesAsync(CurrentUser.Id);
                CanViewAdvancedStats = await _ambassadorAccessService.CanViewAdvancedStatsAsync(CurrentUser.Id);
                CanWithdraw = await _ambassadorAccessService.CanWithdrawAsync(CurrentUser.Id);
                
                // Déterminer la raison du refus d'accès (si applicable)
                if (!CanViewCommissions)
                {
                    AccessDenialReason = "Veuillez souscrire à un plan pour accéder à cette fonctionnalité";
                }

                // Générer le lien de parrainage
                Link = !string.IsNullOrEmpty(CurrentUser.ReferralCode)
                    ? $"{Request.Scheme}://{Request.Host}/Account/SignUp?referralCode={CurrentUser.ReferralCode}"
                    : string.Empty;

                // Date de fin de l'abonnement
                if (DashboardData?.ActiveSubscription != null)
                {
                    CountdownEndDate = DashboardData.ActiveSubscription.EndDate.ToString("yyyy-MM-dd HH:mm:ss");
                }

                // Remplir les ViewData pour compatibilité avec les vues
                await LoadViewDataAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du dashboard User pour {UserId}", CurrentUser?.Id);
                ViewData["ErrorMessage"] = "Une erreur s'est produite lors du chargement du dashboard";
                
                // Initialiser les données par défaut
                DashboardData = new UserDashboardData();
                CanViewCommissions = false;
                CanViewAdvancedStats = false;
                CanWithdraw = false;
                
                return Page();
            }
        }

        private async Task LoadViewDataAsync()
        {
            if (DashboardData == null) return;

            // Objectifs et progress ventes
            var salesProgress = DashboardData.SalesTarget > 0 
                ? (int)((DashboardData.CurrentSalesCommission / DashboardData.SalesTarget) * 100)
                : 0;
            ViewData["SalesCommissions"] = DashboardData.CurrentSalesCommission;
            ViewData["SalesTarget"] = DashboardData.SalesTarget;
            ViewData["SalesProgress"] = Math.Min(salesProgress, 100);

            // Objectifs et progress parrainages
            var referralProgress = DashboardData.ReferralTarget > 0 
                ? (int)((DashboardData.CurrentReferrals / (decimal)DashboardData.ReferralTarget) * 100)
                : 0;
            ViewData["ActiveReferrals"] = DashboardData.CurrentReferrals;
            ViewData["NewReferrals"] = DashboardData.NewReferralsThisMonth;
            ViewData["ReferralTarget"] = DashboardData.ReferralTarget;
            ViewData["ReferralProgress"] = Math.Min(referralProgress, 100);

            // Soldes portefeuille
            ViewData["AvailableBalance"] = DashboardData.AvailableBalance;
            ViewData["PendingBalance"] = DashboardData.PendingBalance;

            // Informations d'abonnement
            ViewData["HasActiveSubscription"] = DashboardData.ActiveSubscription?.Status == SubscriptionStatus.Active;
            ViewData["PlanName"] = DashboardData.CurrentPlan?.Name ?? "Aucun plan";
            ViewData["PlanBonus"] = DashboardData.CurrentPlan != null ? (DashboardData.CurrentPlan.AmbassadorCommissionPercentage * 100) : 0;

            // Code de parrainage
            var referralCode = CurrentUser?.ReferralCode ?? "REF" + CurrentUser?.Id;
            ViewData["ReferralCode"] = referralCode;

            // Calcul des statistiques avancées
            var currentMonth = DateTimeOffset.UtcNow.Month;
            var currentYear = DateTimeOffset.UtcNow.Year;

            // Gains mensuels par type (directs et indirects)
            ViewData["DirectReferrals"] = DashboardData.CurrentReferrals;
            ViewData["IndirectReferrals"] = 0; // Simplification - à améliorer si arborescence existe

            // Revenus mensuels de parrainage
            var referralEarnings = await _context.AmbassadorCommissions
                .AsNoTracking()
                .Where(c => c.AmbassadorId == CurrentUser.Id
                    && c.Status == AmbassadorCommissionStatus.Paid
                    && c.PaidAt.HasValue
                    && c.PaidAt.Value.Month == currentMonth
                    && c.PaidAt.Value.Year == currentYear)
                .SumAsync(c => c.Amount);
            ViewData["MonthlyReferralEarnings"] = referralEarnings;

            // Croissance par rapport au mois précédent
            var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
            var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;

            var previousReferralEarnings = await _context.AmbassadorCommissions
                .AsNoTracking()
                .Where(c => c.AmbassadorId == CurrentUser.Id
                    && c.Status == AmbassadorCommissionStatus.Paid
                    && c.PaidAt.HasValue
                    && c.PaidAt.Value.Month == previousMonth
                    && c.PaidAt.Value.Year == previousYear)
                .SumAsync(c => c.Amount);

            ViewData["ReferralGrowth"] = previousReferralEarnings > 0 
                ? ((referralEarnings - previousReferralEarnings) / previousReferralEarnings) * 100 
                : 0;

            // Statistiques de conversion
            ViewData["TotalVisitsLink"] = 0;
            ViewData["ConversionRate"] = 0;
        }

        public async Task<IActionResult> OnPostCancelPlanAsync()
        {
            _logger.LogInformation("=== OnPostCancelPlanAsync DÉBUT ===");
            
            CurrentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            
            if (CurrentUser == null)
            {
                _logger.LogWarning("Utilisateur non trouvé");
                return RedirectToPage("/Account/Login");
            }

            _logger.LogInformation("Utilisateur trouvé: {UserId}", CurrentUser.Id);

            // Charger directement l'abonnement actif depuis la base de données
            var activeSubscription = await _context.AmbassadorSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == CurrentUser.Id && s.Status == SubscriptionStatus.Active);

            if (activeSubscription == null)
            {
                _logger.LogWarning("Aucun abonnement actif trouvé pour l'utilisateur {UserId}", CurrentUser.Id);
                TempData["ErrorMessage"] = "Aucun abonnement actif à désactiver.";
                return RedirectToPage();
            }

            _logger.LogInformation("Abonnement trouvé: {SubscriptionId} avec le statut: {Status}", activeSubscription.Id, activeSubscription.Status);

            // Mise à jour en base de données
            try
            {
                // Annulation de l'abonnement
                activeSubscription.Status = SubscriptionStatus.Cancelled;
                _logger.LogInformation("Statut de l'abonnement changé à: {NewStatus}", activeSubscription.Status);
                
                // Mettre à jour le statut IsAmbassador sur l'utilisateur
                CurrentUser.IsAmbassador = false;
                _logger.LogInformation("IsAmbassador mis à jour à false");

                // Sauvegarder une seule fois
                await _context.SaveChangesAsync();
                _logger.LogInformation("Changements sauvegardés avec succès");

                // Retirer l'utilisateur du rôle Ambassador s'il en faisait partie
                var userRoles = await _userManager.GetRolesAsync(CurrentUser);
                _logger.LogInformation("Rôles actuels de l'utilisateur: {Roles}", string.Join(", ", userRoles));
                
                if (userRoles.Contains("Ambassador"))
                {
                    _logger.LogInformation("Retrait du rôle Ambassador en cours...");
                    var removeResult = await _userManager.RemoveFromRoleAsync(CurrentUser, "Ambassador");
                    if (!removeResult.Succeeded)
                    {
                        _logger.LogWarning("Impossible de retirer le rôle Ambassador pour l'utilisateur {UserId}. Erreurs: {Errors}", 
                            CurrentUser.Id, 
                            string.Join(", ", removeResult.Errors.Select(e => e.Description)));
                    }
                    else
                    {
                        _logger.LogInformation("Rôle Ambassador retiré avec succès");
                    }
                }

                _logger.LogInformation("=== Abonnement annulé et rôle Ambassador retiré pour l'utilisateur {UserId} ===", CurrentUser.Id);
                TempData["SuccessMessage"] = "Votre plan d'abonnement a été désactivé avec succès.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERREUR lors de l'annulation du plan pour {UserId} ===", CurrentUser.Id);
                TempData["ErrorMessage"] = "Une erreur s'est produite lors de la désactivation du plan: " + ex.Message;
            }

            return RedirectToPage();
        }
    }
}


