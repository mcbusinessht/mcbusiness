using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.UserActivityLogs
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public UserActivityLog UserActivityLog { get; set; } = default!;

        public int? PreviewCount { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var useractivitylog = await _context.UserActivityLogs.FirstOrDefaultAsync(m => m.Id == id);

            if (useractivitylog == null)
            {
                return NotFound();
            }
            else
            {
                UserActivityLog = useractivitylog;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var useractivitylog = await _context.UserActivityLogs.FindAsync(id);
            if (useractivitylog != null)
            {
                UserActivityLog = useractivitylog;
                _context.UserActivityLogs.Remove(UserActivityLog);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnPostBulkDeleteAsync(string deleteOption, 
            string specificDate, string monthYear, string userName)
        {
            IQueryable<UserActivityLog> logsToDelete = _context.UserActivityLogs;

            switch (deleteOption)
            {
                case "all":
                    // Rien à filtrer - on prend tout
                    break;
                case "date":
                    if (DateTimeOffset.TryParse(specificDate, out var date))
                    {
                        logsToDelete = logsToDelete.Where(x => x.Timestamp.Date == date.Date);
                    }
                    break;
                case "month":
                    if (DateTimeOffset.TryParse(monthYear + "-01", out var firstDayOfMonth))
                    {
                        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                        logsToDelete = logsToDelete.Where(x => x.Timestamp >= firstDayOfMonth && 
                                                              x.Timestamp <= lastDayOfMonth);
                    }
                    break;
                case "user":
                    if (!string.IsNullOrEmpty(userName))
                    {
                        logsToDelete = logsToDelete.Where(x => x.UserName == userName);
                    }
                    break;
                default:
                    // Par défaut, on ne supprime que l'entrée actuelle
                    if (UserActivityLog?.Id != null)
                    {
                        logsToDelete = logsToDelete.Where(x => x.Id == UserActivityLog.Id);
                    }
                    break;
            }

            _context.UserActivityLogs.RemoveRange(logsToDelete);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnGetPreviewDeleteAsync(string option, string value)
        {
            IQueryable<UserActivityLog> logsToDelete = _context.UserActivityLogs;
            var previewMessage = "";

            switch (option)
            {
                case "all":
                    previewMessage = $"Vous êtes sur le point de supprimer TOUS les logs ({await logsToDelete.CountAsync()} entrées).";
                    break;
                case "date":
                    if (DateTimeOffset.TryParse(value, out var date))
                    {
                        var count = await logsToDelete.CountAsync(x => x.Timestamp.Date == date.Date);
                        previewMessage = $"Vous êtes sur le point de supprimer {count} entrées pour la date {date.ToString("dd/MM/yyyy")}.";
                    }
                    break;
                case "month":
                    if (DateTimeOffset.TryParse(value + "-01", out var firstDayOfMonth))
                    {
                        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                        var count = await logsToDelete.CountAsync(x => x.Timestamp >= firstDayOfMonth && 
                                                                       x.Timestamp <= lastDayOfMonth);
                        previewMessage = $"Vous êtes sur le point de supprimer {count} entrées pour le mois {firstDayOfMonth:MMMM yyyy}.";
                    }
                    break;
                case "user":
                    if (!string.IsNullOrEmpty(value))
                    {
                        var count = await logsToDelete.CountAsync(x => x.UserName == value);
                        previewMessage = $"Vous êtes sur le point de supprimer {count} entrées pour l'utilisateur '{value}'.";
                    }
                    break;
            }

            if (string.IsNullOrEmpty(previewMessage))
            {
                previewMessage = "Aucune entrée ne correspond aux critères de suppression.";
            }

            return Content($"<div class='alert alert-warning'><i class='bi bi-exclamation-triangle-fill me-2'></i>{previewMessage}</div>");
        }
    }
}
