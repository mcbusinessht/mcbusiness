using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.AmbassadorSpace
{
    [Authorize(Policy = "AmbassadorPolicy")]
    public class AmbassadorCommissionsModel : PageModel
    {
        private readonly AppDbContext _context;

        public AmbassadorCommissionsModel(AppDbContext context)
        {
            _context = context;
        }

        public class SummaryDto
        {
            public decimal total { get; set; }
            public decimal pending { get; set; }
            public decimal approved { get; set; }
            public decimal paid { get; set; }
            public List<MonthPoint> months { get; set; } = new();
        }
        public class MonthPoint { public string label { get; set; } = ""; public decimal amount { get; set; } }

        public async Task<IActionResult> OnGetAsync()
        {
            // Rendu initial de la page (les données dynamiques sont chargées en AJAX)
            return Page();
        }

        // /AmbassadorCommissions?handler=Summary
        public async Task<IActionResult> OnGetSummaryAsync()
        {
            var userName = User?.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return new JsonResult(new SummaryDto());
            var userId = await _context.Users.Where(u => u.UserName == userName).Select(u => u.Id).FirstOrDefaultAsync();
            if (userId == null) return new JsonResult(new SummaryDto());

            var q = _context.AmbassadorCommissions.AsNoTracking().Where(c => !c.IsDeleted && c.AmbassadorId == userId);

            var dto = new SummaryDto
            {
                total = await q.SumAsync(c => (decimal?)c.Amount) ?? 0m,
                pending = await q.Where(c => c.Status == AmbassadorCommissionStatus.Pending).SumAsync(c => (decimal?)c.Amount) ?? 0m,
                approved = await q.Where(c => c.Status == AmbassadorCommissionStatus.Available
                    || c.Status == AmbassadorCommissionStatus.Approved).SumAsync(c => (decimal?)c.Amount) ?? 0m,
                paid = await q.Where(c => c.Status == AmbassadorCommissionStatus.Paid).SumAsync(c => (decimal?)c.Amount) ?? 0m,
            };

            // 12 derniers mois
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
            var monthly = await q
                .Where(c => c.CreatedAt >= start)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Amount) })
                .ToListAsync();

            for (int i = 0; i < 12; i++)
            {
                var d = start.AddMonths(i);
                var found = monthly.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month);
                dto.months.Add(new MonthPoint
                {
                    label = d.ToString("MMM yyyy"),
                    amount = found?.Amount ?? 0m
                });
            }

            return new JsonResult(dto);
        }

        public class ListResult
        {
            public int page { get; set; }
            public int totalPages { get; set; }
            public List<Item> items { get; set; } = new();
            public class Item
            {
                public int Id { get; set; }
                public int? OrderId { get; set; }
                public int Level { get; set; }
                public decimal Amount { get; set; }
                public string Status { get; set; } = string.Empty;
                public DateTimeOffset CreatedAt { get; set; }
            }
        }

        // /AmbassadorCommissions?handler=List&page=1&pageSize=20&status=Approved
        public async Task<IActionResult> OnGetListAsync(int page = 1, int pageSize = 20, string? status = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var userName = User?.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return new JsonResult(new ListResult());
            var userId = await _context.Users.Where(u => u.UserName == userName).Select(u => u.Id).FirstOrDefaultAsync();
            if (userId == null) return new JsonResult(new ListResult());

            var q = _context.AmbassadorCommissions.AsNoTracking().Where(c => !c.IsDeleted && c.AmbassadorId == userId);
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<AmbassadorCommissionStatus>(status, true, out var st))
                {
                    q = q.Where(c => c.Status == st);
                }
            }

            var total = await q.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var items = await q.OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ListResult.Item
                {
                    Id = c.Id,
                    OrderId = c.OrderId,
                    Level = 1, // Generation supprimée
                    Amount = c.Amount,
                    Status = c.Status.ToString(),
                    CreatedAt = c.CreatedAt
                }).ToListAsync();

            return new JsonResult(new ListResult { page = page, totalPages = totalPages, items = items });
        }
    }
}
