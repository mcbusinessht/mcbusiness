using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Admin
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

        [BindProperty]
        public ApplicationUser CurrentUser { get; set; }
        
        // Données dynamiques du dashboard
        public AdminDashboardData DashboardData { get; set; }
        
        // Propriétés pour compatibilité avec les vues existantes
        public int countUser => DashboardData?.TotalUsers ?? 0;
        public int countActiveSub => DashboardData?.ActiveSubscriptions ?? 0;
        public int countInactiveSub => DashboardData?.InactiveSubscriptions ?? 0;
        public int countCancelledSub => DashboardData?.CancelledSubscriptions ?? 0;
        public decimal Solde => DashboardData?.AdminBalance ?? 0;
        public List<Transaction> RecentTransactions => DashboardData?.RecentTransactions ?? new List<Transaction>();
        public int countTrans => DashboardData?.TotalTransactions ?? 0;
        public int countTransEnd => DashboardData?.CompletedTransactions ?? 0;
        public int countTransPend => DashboardData?.PendingTransactions ?? 0;
        public int countTransFailed => DashboardData?.FailedTransactions ?? 0;
        public int countPlan => DashboardData?.TotalPlans ?? 0;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Charger l'utilisateur actuel
                CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
                
                if (CurrentUser == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                // Charger toutes les données du dashboard via le service optimisé
                DashboardData = await _dashboardService.GetAdminDashboardDataAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du dashboard Admin");
                
                // En cas d'erreur, initialiser des données par défaut
                DashboardData = new AdminDashboardData();
                
                return Page();
            }
        }
    }
}

