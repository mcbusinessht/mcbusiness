// Pages/Users/Seller/Dashboard.cshtml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Users.Seller
{
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;
        private readonly AppDbContext _context;
        private readonly IDashboardService _dashboardService;

        public DashboardModel(ILogger<DashboardModel> logger, AppDbContext context, IDashboardService dashboardService)
        {
            _logger = logger;
            _context = context;
            _dashboardService = dashboardService;
        }

        public ApplicationUser CurrentUser { get; set; }
        public SellerDashboardData DashboardData { get; set; }
        
        // Propriétés pour compatibilité avec les vues existantes
        public Company Company => DashboardData?.Company;
        public List<string> SalesLabels => DashboardData?.SalesLabels ?? new();
        public List<decimal> SalesValues => DashboardData?.SalesValues ?? new();
        public List<string> DistLabels => DashboardData?.CategoryLabels ?? new();
        public List<decimal> DistValues => DashboardData?.CategoryValues ?? new();
        public int ProductSold => DashboardData?.ProductsSold ?? 0;
        public decimal GrossAmount => DashboardData?.TotalRevenue ?? 0;
        public int ClicksOnProduct => DashboardData?.ProductViews ?? 0;
        public decimal Rate => DashboardData?.AverageRating ?? 0;
        public int RateCount => DashboardData?.TotalReviews ?? 0;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Charger l'utilisateur actuel
                CurrentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

                if (CurrentUser == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                // Charger toutes les données du dashboard via le service optimisé
                DashboardData = await _dashboardService.GetSellerDashboardDataAsync(CurrentUser.Id);

                // Vérifier si l'utilisateur a une entreprise
                if (DashboardData.Company == null)
                {
                    return Redirect("./NoCompany");
                }

                // Charger les alertes spécifiques au fournisseur
                await LoadSellerAlerts();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du dashboard Seller pour {UserId}", CurrentUser?.Id);
                
                // En cas d'erreur, initialiser des données par défaut
                DashboardData = new SellerDashboardData();
                
                return Page();
            }
        }
        
        private async Task LoadSellerAlerts()
        {
            if (DashboardData?.Company == null) return;

            // Utiliser les données du dashboard pour les alertes
            ViewData["LowStockProducts"] = DashboardData.LowStockProducts;
            ViewData["PendingOrders"] = DashboardData.PendingOrders;
            ViewData["PendingPayments"] = DashboardData.PendingPayments;

            // Calculer d'autres alertes si nécessaire
            var companyId = DashboardData.Company.Id;
            
            // Produits sans stock
            var outOfStockProducts = await _context.Stocks
                .Where(s => s.ProductVariant.Product.CompanyId == companyId && s.Quantity == 0)
                .CountAsync();
            ViewData["OutOfStockProducts"] = outOfStockProducts;

            // Nouveaux avis � traiter (simplification car les propriétés varient)
            var newReviews = await _context.ProductReviews
                .Where(r => r.Product.CompanyId == companyId)
                .CountAsync();
            ViewData["NewReviews"] = newReviews;

            // Retours/réclamations en attente
            var pendingReturns = 0; // Valeur par défaut car le modèle peut ne pas exister
            try 
            {
                // Simplification car ReturnStatus peut ne pas exister
                pendingReturns = 0;
            }
            catch (Exception)
            {
                // Si le modèle ReturnRequest n'existe pas encore
                pendingReturns = 0;
            }
            ViewData["PendingReturns"] = pendingReturns;
        }
    }
}
