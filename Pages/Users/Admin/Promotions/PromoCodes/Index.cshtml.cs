using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Promotions_PromoCodes
{
    public class IndexModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public IndexModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IList<PromoCode> PromoCode { get;set; } = default!;

        public async Task OnGetAsync()
        {
            PromoCode = await _context.PromoCodes
                .Include(p => p.CreatedByUser)
                .Include(p => p.PromoCampaign).ToListAsync();
        }
    }
}
