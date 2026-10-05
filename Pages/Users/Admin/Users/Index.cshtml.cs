using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Identity;

namespace MCBusiness.Pages.Users.Admin.Users
{
    public class IndexModel : PageModel   
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<ApplicationUser> ApplicationUser { get; set; } = default!;

        [BindProperty]
        public string ValueS { get; set; } = string.Empty;

        [BindProperty]
        public string EmailStatusFilter { get; set; } = string.Empty;

        [BindProperty]
        public string RegistrationDateFilter { get; set; } = string.Empty;

        [BindProperty]
        public decimal? MinBalanceFilter { get; set; }

        [BindProperty]
        public decimal? MaxBalanceFilter { get; set; }
        public bool IsLocked { get; set; }

        public async Task OnGetAsync()
        {
            await LoadUsers();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadUsers();
            return Page();
        }

        private async Task LoadUsers()
        {
            var query = _userManager.Users.AsQueryable();

            // Filtre de recherche rapide
            if (!string.IsNullOrEmpty(ValueS))
            {
                query = query.Where(u =>
                    u.FirstName.Contains(ValueS) ||
                    u.LastName.Contains(ValueS) ||
                    u.Email.Contains(ValueS) ||
                    u.PhoneNumber.Contains(ValueS) ||
                    u.UserName.Contains(ValueS));
            }

            // Filtres avancés
            if (!string.IsNullOrEmpty(EmailStatusFilter))
            {
                query = EmailStatusFilter switch
                {
                    "confirmed" => query.Where(u => u.EmailConfirmed),
                    "unconfirmed" => query.Where(u => !u.EmailConfirmed),
                    _ => query
                };
            }

            if (!string.IsNullOrEmpty(RegistrationDateFilter))
            {
                var today = DateTime.Today;
                query = RegistrationDateFilter switch
                {
                    "today" => query.Where(u => u.RegistrationDate.Date == today),
                    "week" => query.Where(u => u.RegistrationDate >= today.AddDays(-7)),
                    "month" => query.Where(u => u.RegistrationDate >= today.AddMonths(-1)),
                    _ => query
                };
            }

            if (MinBalanceFilter.HasValue)
            {
                query = query.Where(u => u.Balance >= MinBalanceFilter.Value);
            }

            if (MaxBalanceFilter.HasValue)
            {
                query = query.Where(u => u.Balance <= MaxBalanceFilter.Value);
            }

            // Exclure les admins et trier par date d'inscription
            var users = await query
                .OrderByDescending(u => u.RegistrationDate)
                .ToListAsync();

            ApplicationUser = new List<ApplicationUser>();
            
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Admin"))
                {
                    ApplicationUser.Add(user);
                }
            }
        }
    }
}
