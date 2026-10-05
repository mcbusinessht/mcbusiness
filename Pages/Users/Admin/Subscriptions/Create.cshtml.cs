using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Subscriptions
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public List<AmbassadorPlan> Plan { get; set; } = new List<AmbassadorPlan>();
        public List<ApplicationUser> ApplicationUser { get; set; } = new List<ApplicationUser>();

        public async Task<IActionResult> OnGet()
        {
            Plan = await _context.AmbassadorPlans.OrderBy(p => p.Name).ToListAsync();
            ApplicationUser = await _context.Users.OrderBy(u => u.LastName).ToListAsync();
            return Page();
        }

        [BindProperty]
        public AmbassadorSubscription Subscription { get; set; } = default!;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || _context.AmbassadorSubscriptions == null || Subscription == null)
            {
                Plan = await _context.AmbassadorPlans.OrderBy(p => p.Name).ToListAsync();
                ApplicationUser = await _context.Users.OrderBy(u => u.UserName).ToListAsync();
                return Page();
            }

            // Calculer la date de fin en fonction du plan sélectionné
            var selectedPlan = await _context.AmbassadorPlans.FindAsync(Subscription.PlanId);
            if (selectedPlan != null)
            {
                Subscription.CalculateEndDate(selectedPlan.DurationInDays);
            }

            // Définir le statut par défaut
            Subscription.Status = SubscriptionStatus.Active;

            _context.AmbassadorSubscriptions.Add(Subscription);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
