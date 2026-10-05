using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Services;


namespace MCBusiness.Pages_Users_Admin_Notifications
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;
        private readonly INotificationService _notificationService;

        public CreateModel(MCBusiness.Models.AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        [BindProperty]
        public NotificationInputModel NotificationInput { get; set; } = new NotificationInputModel();

        public IActionResult OnGet()
        {
            ViewData["Users"] = new MultiSelectList(_context.Users.OrderBy(u => u.UserName), "Id", "UserName");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["Users"] = new MultiSelectList(_context.Users.OrderBy(u => u.UserName), "Id", "UserName");
                return Page();
            }

            if (NotificationInput.SendToAll)
            {
                // Envoyer � tous les utilisateurs
                var allUsers = await _context.Users.ToListAsync();
                foreach (var user in allUsers)
                {
                    var notification = new Notification
                    {
                        UserId = user.Id,
                        Message = NotificationInput.Message,
                        Title = NotificationInput.Title,
                        NotificationType = NotificationInput.NotificationType,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsRead = false
                    };
                    await _notificationService.NotifyAsync(notification); 
                }
            }
            else if (NotificationInput.SelectedUserIds != null && NotificationInput.SelectedUserIds.Any())
            {
                // Envoyer aux utilisateurs sélectionnés
                foreach (var userId in NotificationInput.SelectedUserIds)
                {
                    var notification = new Notification
                    {
                        UserId = userId,
                        Message = NotificationInput.Message,
                        Title = NotificationInput.Title,
                        NotificationType = NotificationInput.NotificationType,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsRead = false
                    };
                    await _notificationService.NotifyAsync(notification); 
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }

    public class NotificationInputModel
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string NotificationType { get; set; } = "general";
        public List<string> SelectedUserIds { get; set; } = new List<string>();
        public bool SendToAll { get; set; }
    }
}
