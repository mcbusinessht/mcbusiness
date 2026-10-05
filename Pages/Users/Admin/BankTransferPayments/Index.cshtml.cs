using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.BankTransferPayments
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<BankTransferPayment> BankTransferPayments { get; set; } = new List<BankTransferPayment>();

        [BindProperty]
        public string SearchValue { get; set; } = string.Empty;

        [BindProperty]
        public string StatusFilter { get; set; }

        [BindProperty]
        public string TransferTypeFilter { get; set; }

        [BindProperty]
        public string DateFilter { get; set; } = string.Empty;

        [BindProperty]
        public decimal? MinAmount { get; set; }

        [BindProperty]
        public decimal? MaxAmount { get; set; }

        public async Task OnGetAsync()
        {
            await LoadPayments();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadPayments();
            return Page();
        }

        private async Task LoadPayments()
        {
            var query = _context.BankTransferPayments
                .Include(p => p.User)
                .AsQueryable();

            // Recherche globale
            if (!string.IsNullOrEmpty(SearchValue))
            {
                query = query.Where(p => 
                    p.User.FirstName.Contains(SearchValue) ||
                    p.User.LastName.Contains(SearchValue) ||
                    p.User.Email.Contains(SearchValue) ||
                    p.BankName.Contains(SearchValue) ||
                    p.AccountNumber.Contains(SearchValue));
            }

            // Filtre par statut
            if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<BankTransferStatus>(StatusFilter, out var status))
            {
                query = query.Where(p => p.Status == status);
            }

            // Filtre par type
            if (!string.IsNullOrEmpty(TransferTypeFilter) && Enum.TryParse<BankTransferMethod>(TransferTypeFilter, out var transferType))
            {
                query = query.Where(p => p.TransferMethod == transferType);
            }

            // Filtre par date
            if (!string.IsNullOrEmpty(DateFilter))
            {
                var today = DateTime.Today;
                query = DateFilter switch
                {
                    "today" => query.Where(p => p.SubmittedAt.Date == today),
                    "week" => query.Where(p => p.SubmittedAt >= today.AddDays(-7)),
                    "month" => query.Where(p => p.SubmittedAt >= today.AddMonths(-1)),
                    _ => query
                };
            }

            // Filtre par montant
            if (MinAmount.HasValue)
            {
                query = query.Where(p => p.Amount >= MinAmount.Value);
            }

            if (MaxAmount.HasValue)
            {
                query = query.Where(p => p.Amount <= MaxAmount.Value);
            }

            BankTransferPayments = await query
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();
        }
    }
}
