using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.AmbassadorSpace
{
    [Authorize(Policy = "AmbassadorPolicy")]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAmbassadorAccessService _ambassadorAccess;
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(
            UserManager<ApplicationUser> userManager,
            IAmbassadorAccessService ambassadorAccess,
            AppDbContext context,
            ILogger<DashboardModel> logger)
        {
            _userManager = userManager;
            _ambassadorAccess = ambassadorAccess;
            _context = context;
            _logger = logger;
        }

        public ApplicationUser CurrentUser { get; set; }
        public bool HasActiveSubscription { get; set; }
        public AmbassadorSubscription ActiveSubscription { get; set; }

        public string ReferralCode { get; set; } = string.Empty;
        public string ReferralLink { get; set; } = string.Empty;
        public string CurrentPlanName { get; set; } = "-";
        public decimal CurrentCommissionRate { get; set; }
        public decimal PlanCeiling { get; set; }
        public int DaysRemaining { get; set; }

        public decimal TotalCommissionsEarned { get; set; }
        public decimal PaidCommissions { get; set; }
        public decimal PendingCommissions { get; set; }
        public decimal ApprovedCommissions { get; set; }
        public decimal AvailableCommissionAmount { get; set; }
        public decimal AvailableCommissions { get; set; }
        public decimal MonthlyEarnings { get; set; }
        public decimal RejectedOrCancelledCommissions { get; set; }

        public int TotalReferrals { get; set; }
        public int ActiveReferrals { get; set; }
        public int MonthlyReferrals { get; set; }
        public int DirectReferralRelations { get; set; }
        public int CookieTrackedCustomers { get; set; }

        public int TotalTrackedOrders { get; set; }
        public int DeliveredTrackedOrders { get; set; }
        public int PaidTrackedOrders { get; set; }
        public decimal TotalTrackedSales { get; set; }
        public decimal MonthlyTrackedSales { get; set; }

        public List<AmbassadorCommission> RecentCommissions { get; set; } = new();
        public List<AmbassadorReferralSnapshot> RecentReferrals { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                CurrentUser = await _userManager.GetUserAsync(User);
                if (CurrentUser == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                HasActiveSubscription = await _ambassadorAccess.CanAccessAmbassadorFeaturesAsync(CurrentUser.Id);
                if (!HasActiveSubscription)
                {
                    TempData["ErrorMessage"] = "Vous n'avez pas d'abonnement ambassadeur actif.";
                    return RedirectToPage("/Plans/Index");
                }

                await LoadAmbassadorDataAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ambassador dashboard for user {UserId}", CurrentUser?.Id);
                TempData["ErrorMessage"] = "Erreur lors du chargement du dashboard.";
                return Page();
            }
        }

        private async Task LoadAmbassadorDataAsync()
        {
            var userId = CurrentUser.Id;
            var now = DateTimeOffset.UtcNow;
            var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

            ReferralCode = CurrentUser.ReferralCode ?? string.Empty;
            ReferralLink = string.IsNullOrWhiteSpace(ReferralCode)
                ? string.Empty
                : $"{Request.Scheme}://{Request.Host}/?ref={Uri.EscapeDataString(ReferralCode)}";

            await LoadSubscriptionAsync(userId, now);
            await LoadCommissionStatsAsync(userId, startOfMonth);
            await LoadReferralAndSalesStatsAsync(userId, startOfMonth);
        }

        private async Task LoadSubscriptionAsync(string userId, DateTimeOffset now)
        {
            ActiveSubscription = await _context.AmbassadorSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.EndDate > now
                    && !s.IsDeleted)
                .Include(s => s.Plan)
                .ThenInclude(p => p.Currency)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            if (ActiveSubscription?.Plan == null)
            {
                return;
            }

            CurrentPlanName = ActiveSubscription.Plan.Name;
            CurrentCommissionRate = ActiveSubscription.Plan.AmbassadorCommissionPercentage;
            PlanCeiling = ActiveSubscription.Plan.Ceiling;
            DaysRemaining = Math.Max(0, (int)Math.Ceiling((ActiveSubscription.EndDate - now).TotalDays));
        }

        private async Task LoadCommissionStatsAsync(string userId, DateTimeOffset startOfMonth)
        {
            var commissions = await _context.AmbassadorCommissions
                .AsNoTracking()
                .Where(c => c.AmbassadorId == userId && !c.IsDeleted)
                .Include(c => c.Currency)
                .Include(c => c.Company)
                .Include(c => c.Order)
                .ThenInclude(o => o.User)
                .ToListAsync();

            TotalCommissionsEarned = commissions
                .Where(c => c.Status != AmbassadorCommissionStatus.Rejected
                    && c.Status != AmbassadorCommissionStatus.Cancelled
                    && c.Status != AmbassadorCommissionStatus.Refunded)
                .Sum(c => c.Amount);

            PaidCommissions = commissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Paid)
                .Sum(c => c.Amount);

            PendingCommissions = commissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Pending)
                .Sum(c => c.Amount);

            ApprovedCommissions = commissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Approved)
                .Sum(c => c.Amount);

            AvailableCommissionAmount = commissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Available)
                .Sum(c => c.Amount);

            RejectedOrCancelledCommissions = commissions
                .Where(c => c.Status == AmbassadorCommissionStatus.Rejected
                    || c.Status == AmbassadorCommissionStatus.Cancelled
                    || c.Status == AmbassadorCommissionStatus.Refunded)
                .Sum(c => c.Amount);

            MonthlyEarnings = commissions
                .Where(c => c.CreatedAt >= startOfMonth
                    && c.Status != AmbassadorCommissionStatus.Rejected
                    && c.Status != AmbassadorCommissionStatus.Cancelled
                    && c.Status != AmbassadorCommissionStatus.Refunded)
                .Sum(c => c.Amount);

            AvailableCommissions = await _context.Wallets
                .AsNoTracking()
                .Where(w => w.UserId == userId && w.Type == WalletType.Earnings && !w.IsDeleted)
                .SumAsync(w => (decimal?)w.Balance) ?? 0m;

            RecentCommissions = commissions
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .ToList();
        }

        private async Task LoadReferralAndSalesStatsAsync(string userId, DateTimeOffset startOfMonth)
        {
            var trackedOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => !o.IsDeleted
                    && o.ReferredByAmbassador == userId)
                .Include(o => o.User)
                .ToListAsync();

            var directReferrals = await _context.Referrals
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.ReferrerId == userId)
                .Include(r => r.Referred)
                .ToListAsync();

            var trackedCustomerIds = trackedOrders
                .Select(o => o.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToHashSet();

            var directReferralIds = directReferrals
                .Select(r => r.ReferredId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToHashSet();

            var allReferralIds = trackedCustomerIds
                .Union(directReferralIds)
                .ToList();

            CookieTrackedCustomers = trackedCustomerIds.Count;
            DirectReferralRelations = directReferralIds.Count;
            TotalReferrals = allReferralIds.Count;
            MonthlyReferrals = CountMonthlyReferrals(trackedOrders, directReferrals, startOfMonth);

            TotalTrackedOrders = trackedOrders.Count;
            DeliveredTrackedOrders = trackedOrders.Count(o => o.Status == OrderStatus.Delivered);
            PaidTrackedOrders = trackedOrders.Count(o =>
                o.Status == OrderStatus.Paid
                || o.Status == OrderStatus.PartiallyShipped
                || o.Status == OrderStatus.Shipped
                || o.Status == OrderStatus.PartiallyDelivered
                || o.Status == OrderStatus.Delivered);

            TotalTrackedSales = trackedOrders
                .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded)
                .Sum(o => o.TotalAmount + o.ShippingTotal);

            MonthlyTrackedSales = trackedOrders
                .Where(o => o.CreatedAt >= startOfMonth
                    && o.Status != OrderStatus.Cancelled
                    && o.Status != OrderStatus.Refunded)
                .Sum(o => o.TotalAmount + o.ShippingTotal);

            ActiveReferrals = allReferralIds.Count == 0
                ? 0
                : await _context.Orders
                    .AsNoTracking()
                    .Where(o => allReferralIds.Contains(o.UserId)
                        && !o.IsDeleted
                        && o.Status != OrderStatus.Pending
                        && o.Status != OrderStatus.Cancelled
                        && o.Status != OrderStatus.Refunded)
                    .Select(o => o.UserId)
                    .Distinct()
                    .CountAsync();

            RecentReferrals = BuildRecentReferralSnapshots(trackedOrders, directReferrals)
                .OrderByDescending(r => r.FirstSeenAt)
                .Take(6)
                .ToList();
        }

        private static int CountMonthlyReferrals(
            List<Order> trackedOrders,
            List<Referral> directReferrals,
            DateTimeOffset startOfMonth)
        {
            var monthlyIds = trackedOrders
                .Where(o => o.CreatedAt >= startOfMonth)
                .Select(o => o.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet();

            foreach (var referral in directReferrals.Where(r => r.ReferredDate >= startOfMonth))
            {
                monthlyIds.Add(referral.ReferredId);
            }

            return monthlyIds.Count;
        }

        private static List<AmbassadorReferralSnapshot> BuildRecentReferralSnapshots(
            List<Order> trackedOrders,
            List<Referral> directReferrals)
        {
            var snapshots = new Dictionary<string, AmbassadorReferralSnapshot>();

            foreach (var group in trackedOrders.GroupBy(o => o.UserId))
            {
                var firstOrder = group.OrderBy(o => o.CreatedAt).First();
                if (firstOrder.User == null)
                {
                    continue;
                }

                snapshots[group.Key] = new AmbassadorReferralSnapshot
                {
                    UserId = group.Key,
                    DisplayName = GetDisplayName(firstOrder.User),
                    Email = firstOrder.User.Email ?? "-",
                    FirstSeenAt = group.Min(o => o.CreatedAt),
                    Source = "Lien de vente",
                    OrderCount = group.Count(),
                    TotalSpent = group
                        .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded)
                        .Sum(o => o.TotalAmount + o.ShippingTotal)
                };
            }

            foreach (var referral in directReferrals)
            {
                if (referral.Referred == null)
                {
                    continue;
                }

                if (snapshots.TryGetValue(referral.ReferredId, out var existing))
                {
                    existing.Source = "Lien de vente + parrainage";
                    if (referral.ReferredDate < existing.FirstSeenAt)
                    {
                        existing.FirstSeenAt = referral.ReferredDate;
                    }

                    continue;
                }

                snapshots[referral.ReferredId] = new AmbassadorReferralSnapshot
                {
                    UserId = referral.ReferredId,
                    DisplayName = GetDisplayName(referral.Referred),
                    Email = referral.Referred.Email ?? "-",
                    FirstSeenAt = referral.ReferredDate,
                    Source = $"Parrainage gen. {referral.Generation}",
                    OrderCount = 0,
                    TotalSpent = 0m
                };
            }

            return snapshots.Values.ToList();
        }

        private static string GetDisplayName(ApplicationUser user)
        {
            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                return user.FullName;
            }

            return user.UserName ?? user.Email ?? "Utilisateur";
        }
    }

    public class AmbassadorReferralSnapshot
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTimeOffset FirstSeenAt { get; set; }
        public string Source { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }
}
