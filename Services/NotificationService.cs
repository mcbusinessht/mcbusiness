using System;
using System.IO;
using System.Threading.Tasks;
using MCBusiness.Interfaces;
using MCBusiness.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(Notification notification);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly NoticeService _noticeService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<NotificationService> logger, NoticeService noticeService)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _noticeService = noticeService;
        }

        public async Task NotifyAsync(Notification notification)
        {
            string userId = notification.UserId;
            string title = notification.Title;

            try
            {
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    _logger.LogWarning($"Aucun utilisateur trouvé pour l'utilisateur {userId}.");
                    return;
                }

                // 1. Sauvegarder la notification en base de données
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Notification sauvegardée pour l'utilisateur {userId}");

                // 2. Envoi de l'email (si l'utilisateur a un email valide)
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogWarning($"Email invalide ou vide pour l'utilisateur {userId} ({user.UserName}). Email non envoyé.");
                    return;
                }

                try
                {
                    var unreadnotifcount = await _noticeService.GetNotificationCountAsync(userId);
                    var emailTemplatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "new_notification.html");
                    
                    if (!File.Exists(emailTemplatePath))
                    {
                        _logger.LogError($"Template email introuvable: {emailTemplatePath}");
                        return;
                    }

                    string html_notification = await File.ReadAllTextAsync(emailTemplatePath);
                    html_notification = html_notification.Replace("{{FullName}}", user.UserName ?? "Utilisateur");
                    html_notification = html_notification.Replace("{{unreadNotifCount}}", unreadnotifcount.ToString());
                    
                    await _emailSender.SendEmailAsync(user.Email, $"Nouvelle notification ({title})", html_notification);
                    _logger.LogInformation($"Email de notification envoyé avec succès � {user.Email}");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Erreur lors de l'envoi de la notification par email à {user.Email}. Message: {e.Message}");
                    // Ne pas propager l'erreur - la notification est déjà sauvegardée en base
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la notification de l'utilisateur {userId}. Message: {ex.Message}");
                throw;
            }
        }
    }
}
