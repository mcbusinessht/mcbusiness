using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Referrals
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public SelectList ReferrersSelectList { get; set; }
        public SelectList ReferredSelectList { get; set; }

        public async Task<IActionResult> OnGet()
        {
            await PrepareSelectLists();
            return Page();
        }

        [BindProperty]
        public Referral Referral { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await PrepareSelectLists();
                return Page();
            }

            _context.Referrals.Add(Referral);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private async Task PrepareSelectLists()
        {
            var referrers = await _context.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            ReferrersSelectList = new SelectList(referrers, "Id", "FullName");

            var referred = await _context.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            ReferredSelectList = new SelectList(referred, "Id", "FullName");

            ViewData["ReferrerId"] = ReferrersSelectList;
            ViewData["ReferredId"] = ReferredSelectList;
        }
    }
}
