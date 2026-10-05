using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Pages.Users.Admin.Subscriptions
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<ApplicationUser> ApplicationUser { get; set; } = default!;
        public IList<AmbassadorSubscription> Subscription { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.AmbassadorSubscriptions != null)
            {
                ApplicationUser = await _context.Users.ToListAsync();
                Subscription = await _context.AmbassadorSubscriptions.Include(s => s.Plan).Include(s => s.User).ToListAsync();


            }
        }
    }
}
