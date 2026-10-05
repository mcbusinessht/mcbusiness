using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;


namespace MCBusiness.Pages.Plans
{
    public class Index : PageModel
    {
        private readonly ILogger<Index> _logger;
        private readonly AppDbContext _context;

        public Index(ILogger<Index> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public List<AmbassadorPlan> plans { get; private set; } = new List<AmbassadorPlan>();
        public Dictionary<int, List<AmbassadorPlanBonus>> PlanBonuses { get; private set; } = new();

        public async Task OnGetAsync()
        {
            plans = await _context.AmbassadorPlans
                .AsNoTracking()
                .Include(p => p.Currency)
                .Include(p => p.Category)
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.IsFreeplan ? 0 : 1)
                .ThenBy(p => p.Price)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var planIds = plans.Select(p => p.Id).ToList();
            if (planIds.Any())
            {
                PlanBonuses = await _context.AmbassadorPlanBonusMappings
                    .AsNoTracking()
                    .Include(m => m.PlanBonus)
                    .Where(m => planIds.Contains(m.AmbassadorPlanId)
                        && !m.IsDeleted
                        && m.PlanBonus != null
                        && !m.PlanBonus.IsDeleted)
                    .GroupBy(m => m.AmbassadorPlanId)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.Select(m => m.PlanBonus).ToList());
            }
        }
    }
}
