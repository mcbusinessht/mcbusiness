using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Transactions_PlatformTransactions
{
    public class IndexModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public IndexModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IList<PlatformTransaction> PlatformTransaction { get;set; } = default!;

        public async Task OnGetAsync()
        {
            PlatformTransaction = await _context.PlatformTransactions
                .Include(p => p.AmbassadorSubscription)
                .Include(p => p.Employee)
                .Include(p => p.Order)
                .Include(p => p.AmbassadorSubscription)
                .Include(p => p.Seller)
                .Include(p => p.Wallet).OrderByDescending(p => p.CreatedAt).ToListAsync();
        }
    }
}
