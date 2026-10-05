using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using MCBusiness.Hubs;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.User
{
    public class NotificationsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILocalTimeService _localTime;

        public NotificationsModel(AppDbContext context, IHubContext<NotificationHub> hubContext, ILocalTimeService localTime)
        {
            _context = context;
            _hubContext = hubContext;
            _localTime = localTime;
        }

        public List<Notification> Notifications { get; set; }
        public ApplicationUser CurrentUser { get; set; }
        public AmbassadorSubscription ActiveSubscription { get; set; }

        // Méthodes pour l'affichage
        public string GetNotificationIcon(string type)
        {
            return type?.ToLower() switch
            {
                "order" => "bi-cart-check-fill",
                "payment" => "bi-credit-card-fill",
                "commission" => "bi-currency-dollar",
                "referral" => "bi-people-fill",
                "system" => "bi-gear-fill",
                "security" => "bi-shield-exclamation",
                "promotion" => "bi-gift-fill",
                "message" => "bi-envelope-fill",
                _ => "bi-bell-fill"
            };
        }

        public string GetNotificationIconColor(string type)
        {
            return type?.ToLower() switch
            {
                "order" => "text-success bg-success bg-opacity-10 rounded-circle p-2",
                "payment" => "text-primary bg-primary bg-opacity-10 rounded-circle p-2",
                "commission" => "text-warning bg-warning bg-opacity-10 rounded-circle p-2",
                "referral" => "text-info bg-info bg-opacity-10 rounded-circle p-2",
                "system" => "text-secondary bg-secondary bg-opacity-10 rounded-circle p-2",
                "security" => "text-danger bg-danger bg-opacity-10 rounded-circle p-2",
                "promotion" => "text-purple bg-purple bg-opacity-10 rounded-circle p-2",
                "message" => "text-primary bg-primary bg-opacity-10 rounded-circle p-2",
                _ => "text-muted bg-light rounded-circle p-2"
            };
        }

        public string GetNotificationTypeBadge(string type)
        {
            return type?.ToLower() switch
            {
                "order" => "bg-success bg-opacity-10 text-success",
                "payment" => "bg-primary bg-opacity-10 text-primary",
                "commission" => "bg-warning bg-opacity-10 text-warning",
                "referral" => "bg-info bg-opacity-10 text-info",
                "system" => "bg-secondary bg-opacity-10 text-secondary",
                "security" => "bg-danger bg-opacity-10 text-danger",
                "promotion" => "bg-purple bg-opacity-10 text-purple",
                "message" => "bg-primary bg-opacity-10 text-primary",
                _ => "bg-light text-muted"
            };
        }

        public string GetNotificationTypeText(string type)
        {
            return type?.ToLower() switch
            {
                "order" => "Commande",
                "payment" => "Paiement",
                "commission" => "Commission",
                "referral" => "Parrainage",
                "system" => "Système",
                "security" => "Sécurité",
                "promotion" => "Promotion",
                "message" => "Message",
                _ => "Notification"
            };
        }

        public string CleanText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var result = value;
            for (var i = 0; i < 3 && LooksMojibake(result); i++)
            {
                try
                {
                    var repaired = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(result));
                    if (string.Equals(repaired, result, StringComparison.Ordinal))
                    {
                        break;
                    }

                    result = repaired;
                }
                catch
                {
                    break;
                }
            }

            return result;
        }

        private static bool LooksMojibake(string value)
        {
            return value.Contains('Ã') ||
                   value.Contains('Â') ||
                   value.Contains('â') ||
                   value.Contains('�') ||
                   value.Contains("ï¿½", StringComparison.Ordinal);
        }

        public string GetRelativeTime(DateTimeOffset DateTimeOffset)
        {
            var timeSpan = DateTimeOffset.UtcNow - DateTimeOffset;
            
            return timeSpan switch
            {
                _ when timeSpan.Days > 7 => _localTime.Format(DateTimeOffset, "dd/MM/yyyy"),
                _ when timeSpan.Days >= 1 => $"Il y a {timeSpan.Days} jour{(timeSpan.Days > 1 ? "s" : "")}",
                _ when timeSpan.Hours >= 1 => $"Il y a {timeSpan.Hours} heure{(timeSpan.Hours > 1 ? "s" : "")}",
                _ when timeSpan.Minutes >= 1 => $"Il y a {timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")}",
                _ => "À l'instant"
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Charger l'utilisateur actuel
            CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (CurrentUser == null)
            {
                return Challenge();
            }

            // Charger l'abonnement actif
            ActiveSubscription = await _context.AmbassadorSubscriptions
                .FirstOrDefaultAsync(s => 
                    s.UserId == userId && 
                    s.Status == SubscriptionStatus.Active);

            Notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnGetNotificationDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification == null) return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return new JsonResult(new
            {
                message = CleanText(notification.Message),
                createdAt = _localTime.Format(notification.CreatedAt, "g")
            });
        }

        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return new OkResult();
        }

        public async Task<IActionResult> OnGetLoadMoreNotificationsAsync(int skip = 0, int take = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var result = notifications.Select(n => new
            {
                n.Id,
                Type = n.NotificationType,
                Title = CleanText(n.Title),
                Message = CleanText(n.Message),
                n.IsRead,
                CreatedAt = _localTime.Format(n.CreatedAt, "g"),
                Icon = GetNotificationIcon(n.NotificationType),
                IconColor = GetNotificationIconColor(n.NotificationType),
                TypeBadge = GetNotificationTypeBadge(n.NotificationType),
                TypeText = GetNotificationTypeText(n.NotificationType),
                RelativeTime = GetRelativeTime(n.CreatedAt)
            }).ToList();

            return new JsonResult(result);
        }
    }
}
