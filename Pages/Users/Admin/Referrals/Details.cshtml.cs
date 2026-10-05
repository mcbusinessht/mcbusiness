using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Referrals
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Referral Referral { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null || _context.Referrals == null)
            {
                return NotFound();
            }

            var referral = await _context.Referrals.Include(r => r.Referrer).Include(r => r.Referred).FirstOrDefaultAsync(m => m.Id == id);
            if (referral == null)
            {
                return NotFound();
            }
            else
            {
                Referral = referral;
            }
            return Page();
        }
    }
}
