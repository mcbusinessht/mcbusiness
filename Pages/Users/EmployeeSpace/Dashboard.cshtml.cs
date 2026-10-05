using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;


namespace MCBusiness.Pages.Users.EmployeeSpace
{
    public class DashboardModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardModel> _logger;

        public ApplicationUser CurrentUser { get; set; }
        public EmployeeDashboardData DashboardData { get; set; }
        
        // Propriétés pour compatibilité avec les vues existantes
        public Employee EmployeeInfo => DashboardData?.Employee;
        public int CompletedTasks => DashboardData?.CompletedTasks ?? 0;
        public int PendingTasks => DashboardData?.PendingTasks ?? 0;
        public string AttendanceRate => $"{DashboardData?.AttendanceRate ?? 0:F1}%";
        public decimal PerformanceScore => DashboardData?.AveragePerformance ?? 0;
        public decimal MonthlyHours => DashboardData?.MonthlyHours ?? 0;
        public decimal HoursTarget { get; set; } = 160m;
        public decimal EstimatedSalary => DashboardData?.EstimatedSalary ?? 0;
        public decimal BaseSalary => DashboardData?.BaseSalary ?? 0;
        public decimal BonusAmount => DashboardData?.BonusAmount ?? 0;
        public bool IsActiveEmployee => DashboardData?.Employee != null && !DashboardData.Employee.IsDeleted;
        public decimal WeeklyHours => DashboardData?.WeeklyHours ?? 0;
        public List<EmployeeTask> RecentTasks => DashboardData?.RecentTasks ?? new List<EmployeeTask>();
        public List<Notification> RecentNotifications { get; set; } = new List<Notification>();
        
        // Charts data
        public List<string> PerformanceLabels => DashboardData?.PerformanceLabels ?? new List<string>();
        public List<decimal> PerformanceValues => DashboardData?.PerformanceValues ?? new List<decimal>();
        public List<string> TaskDistributionLabels => DashboardData?.TaskStatusLabels ?? new List<string>();
        public List<int> TaskDistributionValues => DashboardData?.TaskStatusValues ?? new List<int>();

        public DashboardModel(AppDbContext context, IDashboardService dashboardService, ILogger<DashboardModel> logger)
        {
            _context = context;
            _dashboardService = dashboardService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToPage("/Account/Login");
                }

                CurrentUser = await _context.Users
                    .Include(u => u.PreferredCurrency)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (CurrentUser == null)
                {
                    return NotFound("Utilisateur non trouvé");
                }

                // Vérifier si l'utilisateur est bien un employé
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == CurrentUser.Id);
                if (employee == null)
                {
                    return RedirectToPage("/Users/EmployeeSpace/NoEmployee");
                }

                // Charger toutes les données du dashboard via le service optimisé
                DashboardData = await _dashboardService.GetEmployeeDashboardDataAsync(CurrentUser.Id);

                if (DashboardData.Employee == null)
                {
                    return RedirectToPage("/Error", new { message = "Profil employé non configuré" });
                }

                // Récupération des notifications récentes pour cet employé
                RecentNotifications = await _context.Notifications
                    .Where(n => n.UserId == CurrentUser.Id && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                // Mise à jour des ViewData pour compatibilité avec les vues existantes
                ViewData["CompletedTasks"] = CompletedTasks;
                ViewData["PendingTasks"] = PendingTasks;
                ViewData["AssignedTasks"] = CompletedTasks + PendingTasks + (DashboardData.InProgressTasks);
                ViewData["AttendanceRate"] = AttendanceRate;
                ViewData["PerformanceScore"] = PerformanceScore;
                ViewData["MonthlyHours"] = MonthlyHours;
                ViewData["HoursTarget"] = HoursTarget;
                ViewData["EstimatedSalary"] = EstimatedSalary;
                ViewData["BaseSalary"] = BaseSalary;
                ViewData["BonusAmount"] = BonusAmount;
                ViewData["WeeklyHours"] = WeeklyHours;

                // Charger les données de pointage du jour
                await LoadTodayClockingDataAsync(CurrentUser.Id);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du dashboard Employee pour {UserId}", CurrentUser?.Id);
                
                // En cas d'erreur, initialiser des données par défaut
                DashboardData = new EmployeeDashboardData();
                RecentNotifications = new List<Notification>();
                
                return Page();
            }
        }

        private async Task LoadTodayClockingDataAsync(string userId)
        {
            var today = DateTimeOffset.UtcNow.Date;
            
            try
            {
                // Essayer de charger l'enregistrement d'attendance du jour
                var todayAttendance = await _context.Set<EmployeeAttendance>()
                    .Where(a => a.Employee.UserId == userId && 
                               a.Date.Date == today)
                    .FirstOrDefaultAsync();

                if (todayAttendance != null)
                {
                    // Charger les heures de pointage si disponibles
                    ViewData["ClockInTime"] = todayAttendance.CheckInTime?.ToString("HH:mm") ?? "--:--";
                    ViewData["ClockOutTime"] = todayAttendance.CheckOutTime?.ToString("HH:mm") ?? "--:--";
                    
                    // Calculer les heures de travail du jour
                    if (todayAttendance.CheckInTime.HasValue && todayAttendance.CheckOutTime.HasValue)
                    {
                        var timeSpan = todayAttendance.CheckOutTime.Value - todayAttendance.CheckInTime.Value;
                        ViewData["TodayHours"] = $"{timeSpan.Hours}h{timeSpan.Minutes}m";
                    }
                    else if (todayAttendance.CheckInTime.HasValue)
                    {
                        var timeSpan = DateTimeOffset.UtcNow - todayAttendance.CheckInTime.Value;
                        ViewData["TodayHours"] = $"{timeSpan.Hours}h{timeSpan.Minutes}m";
                    }
                    else
                    {
                        ViewData["TodayHours"] = "0h";
                    }

                    ViewData["IsClockedIn"] = todayAttendance.CheckInTime.HasValue && !todayAttendance.CheckOutTime.HasValue;
                }
                else
                {
                    // Pas de données d'attendance pour aujourd'hui
                    ViewData["ClockInTime"] = "--:--";
                    ViewData["ClockOutTime"] = "--:--";
                    ViewData["TodayHours"] = "0h";
                    ViewData["IsClockedIn"] = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors de la charge des données de pointage pour {UserId}", userId);
                
                // Valeurs par défaut en cas d'erreur
                ViewData["ClockInTime"] = "--:--";
                ViewData["ClockOutTime"] = "--:--";
                ViewData["TodayHours"] = "0h";
                ViewData["IsClockedIn"] = false;
            }
        }
    }
}
