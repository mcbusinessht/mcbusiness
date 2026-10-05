using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Users
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;  

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ApplicationUser ApplicationUser { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (ApplicationUser == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            
            if (user != null)
            {
                // Soft delete - mark as deleted instead of removing
                user.IsDeleted = true;
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue; // Lock permanently
                
                // Optionally deactivate related entities
                var wallets = await _context.Wallets.Where(w => w.UserId == user.Id).ToListAsync();
                foreach (var wallet in wallets)
                {
                    wallet.IsDeleted = true;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
