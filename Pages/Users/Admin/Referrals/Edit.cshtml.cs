using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Referrals
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Referral Referral { get; set; }

        public SelectList ReferrersSelectList { get; set; }
        public SelectList ReferredSelectList { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Referral = await _context.Referrals
                .Include(r => r.Referrer)
                .Include(r => r.Referred)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Referral == null)
            {
                return NotFound();
            }

            await PrepareSelectLists();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await PrepareSelectLists();
                return Page();
            }

            _context.Attach(Referral).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReferralExists(Referral.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private async Task PrepareSelectLists()
        {
            var referrers = await _context.Users
                .OrderBy(u => u.LastName)
                .ToListAsync();

            ReferrersSelectList = new SelectList(referrers, "Id", "FullName", Referral.ReferrerId);

            var referred = await _context.Users
                .OrderBy(u => u.LastName)
                .ToListAsync();

            ReferredSelectList = new SelectList(referred, "Id", "FullName", Referral.ReferredId);

            ViewData["ReferrerId"] = ReferrersSelectList;
            ViewData["ReferredId"] = ReferredSelectList;
        }

        private bool ReferralExists(int id)
        {
            return _context.Referrals.Any(e => e.Id == id);
        }
    }
}
