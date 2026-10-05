using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Subscriptions
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public AmbassadorSubscription Subscription { get; set; } = default!;

        [BindProperty]
        public int SelectedPlanId { get; set; }

        [BindProperty]
        public string SelectedUserId { get; set; }

        public SelectList UsersSelectList { get; set; }
        public SelectList PlansSelectList { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null || _context.AmbassadorSubscriptions == null)
            {
                return NotFound();
            }

            // Charger l'abonnement avec les relations utilisateur et plan
            var subscription = await _context.AmbassadorSubscriptions
                .Include(s => s.User)
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            Subscription = subscription;
            SelectedPlanId = subscription.PlanId;
            SelectedUserId = subscription.UserId;

            // Préparer les listes déroulantes
            await PrepareSelectLists();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await PrepareSelectLists();
                return Page();
            }

            // Charger l'abonnement existant pour éviter la surécriture
            var existingSubscription = await _context.AmbassadorSubscriptions
                .FirstOrDefaultAsync(s => s.Id == Subscription.Id);

            if (existingSubscription == null)
            {
                return NotFound();
            }

            // Mettre à jour uniquement les champs modifiables
            existingSubscription.UserId = SelectedUserId;
            existingSubscription.PlanId = SelectedPlanId;
            existingSubscription.StartDate = Subscription.StartDate;
            existingSubscription.Status = Subscription.Status;

            // Recalculer la date de fin si le plan a changé
            var selectedPlan = await _context.AmbassadorPlans.FindAsync(SelectedPlanId);
            if (selectedPlan != null)
            {
                existingSubscription.CalculateEndDate(selectedPlan.DurationInDays);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SubscriptionExists(Subscription.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private async Task PrepareSelectLists()
        {
            // Liste des utilisateurs
            var users = await _context.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            UsersSelectList = new SelectList(users, "Id", "UserName", SelectedUserId);

            // Liste des plans
            var plans = await _context.AmbassadorPlans
                .OrderBy(p => p.Name)
                .ToListAsync();

            PlansSelectList = new SelectList(plans, "Id", "Name", SelectedPlanId);

            ViewData["UserId"] = UsersSelectList;
            ViewData["PlanId"] = PlansSelectList;
        }

        private bool SubscriptionExists(int id)
        {
            return _context.AmbassadorSubscriptions.Any(e => e.Id == id);
        }
    }
}
