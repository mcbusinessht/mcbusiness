using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Promotions
{
    [Authorize(Roles = "Admin")]
    public class PromoAnalyticsModel : PageModel
    {
        private readonly AppDbContext _context;

        public PromoAnalyticsModel(AppDbContext context)
        {
            _context = context;
        }

        // Propriétés pour les filtres
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CampaignId { get; set; }

        // KPIs globaux
        public int TotalUsages { get; set; }
        public decimal TotalPromoDiscount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageDiscountRate { get; set; }

        // Données pour les tableaux et graphiques
        public List<CampaignStatViewModel> CampaignStats { get; set; } = new();
        public List<PromoCampaign> Campaigns { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // Charger toutes les campagnes pour le filtre
            Campaigns = await _context.PromoCampaigns
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Définir les dates par défaut (30 derniers jours)
            if (!StartDate.HasValue)
                StartDate = DateTime.UtcNow.AddDays(-30);
            if (!EndDate.HasValue)
                EndDate = DateTime.UtcNow;

            // Requête de base pour les usages de codes promo
            var usagesQuery = _context.PromoCodeUsages
                .Include(pcu => pcu.PromoCode)
                    .ThenInclude(pc => pc.PromoCampaign)
                .Where(pcu => pcu.UsedAt >= StartDate 
                    && pcu.UsedAt <= EndDate);

            // Filtrer par campagne si spécifié
            if (CampaignId.HasValue)
            {
                usagesQuery = usagesQuery.Where(pcu => pcu.PromoCode.PromoCampaignId == CampaignId.Value);
            }

            var usages = await usagesQuery.ToListAsync();

            // Calculer les KPIs globaux
            TotalUsages = usages.Count;
            TotalPromoDiscount = usages.Sum(u => u.DiscountAmount);

            // Calculer les statistiques par campagne
            var campaignGroups = usages
                .GroupBy(u => new { u.PromoCode.PromoCampaignId, u.PromoCode.PromoCampaign, u.PromoCodeId, u.PromoCode })
                .Select(g => new
                {
                    Campaign = g.Key.PromoCampaign,
                    PromoCode = g.Key.PromoCode,
                    Usages = g.ToList()
                })
                .ToList();

            foreach (var group in campaignGroups)
            {
                // Récupérer les paiements associés pour calculer le revenu
                var promoCodeIds = new[] { group.PromoCode.Id };
                var payments = await _context.Payments
                    .Include(p => p.Order)
                    .Include(p => p.AmbassadorSubscription)
                        .ThenInclude(s => s.Plan)
                    .Where(p => p.PromoCodeUsageId != null 
                        && _context.PromoCodeUsages.Any(pcu => 
                            pcu.Id == p.PromoCodeUsageId 
                            && pcu.PromoCodeId == group.PromoCode.Id))
                    .ToListAsync();

                var revenue = payments.Sum(p => p.Amount);

                // Vérifier le statut d'audit
                var auditedCount = await _context.PlatformTransactions
                    .Where(pt => pt.Category == PlatformTransactionCategory.PromoDiscount
                        && !pt.IsDeleted
                        && pt.CreatedAt >= StartDate
                        && pt.CreatedAt <= EndDate
                        && pt.Description.Contains(group.PromoCode.Code))
                    .CountAsync();

                CampaignStats.Add(new CampaignStatViewModel
                {
                    CampaignName = group.Campaign.Name,
                    PromoCode = group.PromoCode.Code,
                    StartDate = group.Campaign.StartDate,
                    EndDate = group.Campaign.EndDate ?? DateTimeOffset.UtcNow,
                    TotalUsages = group.Usages.Count,
                    DiscountCost = group.Usages.Sum(u => u.DiscountAmount),
                    Revenue = revenue,
                    AuditedUsages = auditedCount
                });
            }

            // Trier par coût décroissant
            CampaignStats = CampaignStats.OrderByDescending(c => c.DiscountCost).ToList();

            // Calculer le revenu total
            TotalRevenue = CampaignStats.Sum(c => c.Revenue);

            // Calculer le taux de remise moyen
            AverageDiscountRate = TotalRevenue > 0 
                ? (TotalPromoDiscount / TotalRevenue * 100) 
                : 0;

            return Page();
        }
    }

    public class CampaignStatViewModel
    {
        public string CampaignName { get; set; }
        public string PromoCode { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public int TotalUsages { get; set; }
        public decimal DiscountCost { get; set; }
        public decimal Revenue { get; set; }
        public int AuditedUsages { get; set; }
    }
}
