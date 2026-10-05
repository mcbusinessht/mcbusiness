using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.Admin.Commissions
{
    [Authorize(Policy = "AdminPolicy")]
    [MCBusiness.Attributes.IdempotentAction(timeoutSeconds: 10, keyPrefix: "UpdateCommission")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AmbassadorCommissionService _commissionService;
        private readonly IWalletService _walletService;

        public IndexModel(AppDbContext context, AmbassadorCommissionService commissionService, IWalletService walletService)
        {
            _context = context;
            _commissionService = commissionService;
            _walletService = walletService;
        }

        public class Row
        {
            public int Id { get; set; }
            public string AmbassadorId { get; set; } = string.Empty;
            public string AmbassadorEmail { get; set; } = string.Empty;
            public int? OrderId { get; set; }
            public decimal Amount { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset? AvailableAt { get; set; }
            public string? ProcessingNotes { get; set; }
        }

        [BindProperty(SupportsGet = true)] public string? FilterAmb { get; set; }
        [BindProperty(SupportsGet = true)] public int? FilterOrderId { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterStatus { get; set; }

        public List<string> StatusOptions { get; } = Enum.GetNames(typeof(AmbassadorCommissionStatus))
            .Where(s => s != nameof(AmbassadorCommissionStatus.Approved))
            .ToList();
        public List<Row> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            var q = _context.AmbassadorCommissions
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(FilterAmb))
            {
                var ambIds = await _context.Users
                    .Where(u => u.UserName.Contains(FilterAmb) || u.Email.Contains(FilterAmb))
                    .Select(u => new { u.Id, u.Email })
                    .ToListAsync();
                var ids = ambIds.Select(a => a.Id).ToList();
                q = q.Where(c => ids.Contains(c.AmbassadorId));
            }

            if (FilterOrderId.HasValue) q = q.Where(c => c.OrderId == FilterOrderId.Value);
            if (!string.IsNullOrWhiteSpace(FilterStatus) && Enum.TryParse<AmbassadorCommissionStatus>(FilterStatus, true, out var st))
                q = q.Where(c => c.Status == st);

            var users = await _context.Users.AsNoTracking().Select(u => new { u.Id, u.Email }).ToListAsync();
            var map = users.ToDictionary(x => x.Id, x => x.Email ?? x.Id);

            Items = await q.OrderByDescending(c => c.CreatedAt)
                .Take(200)
                .Select(c => new Row
                {
                    Id = c.Id,
                    AmbassadorId = c.AmbassadorId,
                    AmbassadorEmail = "",
                    OrderId = c.OrderId,
                    Amount = c.Amount,
                    Status = c.Status.ToString(),
                    CreatedAt = c.CreatedAt,
                    AvailableAt = c.AvailableAt,
                    ProcessingNotes = c.ProcessingNotes
                }).ToListAsync();

            foreach (var it in Items)
            {
                if (map.TryGetValue(it.AmbassadorId, out var email)) it.AmbassadorEmail = email;
            }
        }

        public class UpdateInput
        {
            public int id { get; set; }
            public string newStatus { get; set; } = string.Empty;
            public string? note { get; set; }
        }

        public async Task<IActionResult> OnPostAsync(int id, string newStatus, string? note)
        {
            if (!Enum.TryParse<AmbassadorCommissionStatus>(newStatus, true, out var target))
                return RedirectToPage();

            var c = await _context.AmbassadorCommissions.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return RedirectToPage();

            // PROTECTION IDEMPOTENCE: Vérifier que la commission peut être payée
            if (target == AmbassadorCommissionStatus.Paid && !MCBusiness.Helpers.IdempotencyHelper.CanPayCommission(c))
            {
                return RedirectToPage();
            }

            if (target == AmbassadorCommissionStatus.Paid)
            {
                await _commissionService.PayAvailableCommissionAsync(c.Id);
                return RedirectToPage(new { amb = FilterAmb, orderId = FilterOrderId, status = FilterStatus });
            }

            // If we cancel after Paid, create a compensating negative wallet transaction
            if (target == AmbassadorCommissionStatus.Cancelled && c.Status == AmbassadorCommissionStatus.Paid)
            {
                var wallet = await _walletService.GetOrCreateWalletAsync(c.AmbassadorId, WalletType.Earnings, c.CurrencyId);
                if (wallet.Balance < c.Amount)
                {
                    ModelState.AddModelError(string.Empty, "Solde gains insuffisant pour annuler cette commission deja payee.");
                    await OnGetAsync();
                    return Page();
                }

                wallet.Balance -= c.Amount;
                wallet.LastUpdated = DateTimeOffset.UtcNow;
                wallet.UpdatedAt = DateTimeOffset.UtcNow;

                var wt = new WalletTransaction
                {
                    WalletId = wallet.Id,
                    UserId = c.AmbassadorId,
                    Amount = -Math.Abs(c.Amount),
                    Type = WalletTransactionType.Refund, // compensation
                    WalletType = WalletType.Earnings,
                    Balance = wallet.Balance,
                    TransactionId = null,
                    Description = $"Commission #{c.Id} annulée (compensation)",
                    CurrencyBase = c.CurrencyId
                };
                _context.WalletTransactions.Add(wt);
            }

            c.Status = target;
            if (!string.IsNullOrWhiteSpace(note))
            {
                c.ProcessingNotes = string.IsNullOrWhiteSpace(c.ProcessingNotes)
                    ? note
                    : c.ProcessingNotes + " | " + note;
            }
            await _context.SaveChangesAsync();
            return RedirectToPage(new { amb = FilterAmb, orderId = FilterOrderId, status = FilterStatus });
        }
    }
}
