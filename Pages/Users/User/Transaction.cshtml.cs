using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.User
{
    public class TransactionModel : PageModel
    {
        
         private readonly ILogger<TransactionModel> _logger;
        private readonly AppDbContext _context;

        public TransactionModel(ILogger<TransactionModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public ApplicationUser CurrentUser { get; set; }
        public List<Transaction> Transactions { get; set; }
        public AmbassadorSubscription ActiveSubscription { get; set; }

        public async Task OnGetAsync()
        {
            CurrentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            
            if (CurrentUser != null)
            {
                Transactions = await _context.Transactions
                    .Where(t => t.UserId == CurrentUser.Id)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync() ?? new List<Transaction>();

                // Charger l'abonnement actif pour la sidebar
                ActiveSubscription = await _context.AmbassadorSubscriptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == CurrentUser.Id && s.Status == SubscriptionStatus.Active);
            }
            else
            {
                Transactions = new List<Transaction>();
            }
        }

        public void OnPost()
        {
        }

        public async Task<IActionResult> OnGetLoadMoreTransactionsAsync(int skip = 0, int take = 20)
        {
            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            if (currentUser == null)
            {
                return Unauthorized();
            }

            var transactions = await _context.Transactions
                .Where(t => t.UserId == currentUser.Id)
                .OrderByDescending(t => t.TransactionDate)
                .Skip(skip)
                .Take(take)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.Amount,
                    t.Currency,
                    t.Description,
                    t.TransactionDate,
                    t.Status
                })
                .ToListAsync();

            return new JsonResult(transactions);
        }
    }
}
