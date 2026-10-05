using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Users
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
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

            ApplicationUser = await _context.Users.FindAsync(id);

            if (ApplicationUser == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Get the existing user from database
                var existingUser = await _context.Users.FindAsync(ApplicationUser.Id);
                
                if (existingUser == null)
                {
                    return NotFound();
                }

                if (ApplicationUser.TwoFactorEnabled && !ApplicationUser.EmailConfirmed)
                {
                    ModelState.AddModelError(
                        "ApplicationUser.TwoFactorEnabled",
                        "L'adresse email doit etre confirmee avant d'activer le 2FA.");
                    return Page();
                }

                // Update only the properties that should be editable
                existingUser.FirstName = ApplicationUser.FirstName;
                existingUser.LastName = ApplicationUser.LastName;
                existingUser.Gender = ApplicationUser.Gender;
                existingUser.ReferralCode = ApplicationUser.ReferralCode;
                existingUser.ReferredBy = ApplicationUser.ReferredBy;
                existingUser.ProfilePictureUrl = ApplicationUser.ProfilePictureUrl;
                existingUser.Email = ApplicationUser.Email;
                existingUser.EmailConfirmed = ApplicationUser.EmailConfirmed;
                existingUser.PhoneNumber = ApplicationUser.PhoneNumber;
                existingUser.PhoneNumberConfirmed = ApplicationUser.PhoneNumberConfirmed;
                existingUser.TwoFactorEnabled = ApplicationUser.TwoFactorEnabled;
                existingUser.LockoutEnd = ApplicationUser.LockoutEnd;
                existingUser.LockoutEnabled = ApplicationUser.LockoutEnabled;
                existingUser.AccessFailedCount = ApplicationUser.AccessFailedCount;

                // Save changes
                await _context.SaveChangesAsync();
                
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!await UserExists(ApplicationUser.Id))
                {
                    return NotFound();
                }
                else
                {
                    ModelState.AddModelError(string.Empty, $" {ex}The record you attempted to edit "
                        + "was modified by another user after you got the original value. The "
                        + "edit operation was canceled and the current values in the database "
                        + "have been displayed. If you still want to edit this record, click "
                        + "the Save button again.");
                    
                    // Reload the current values from database
                    ApplicationUser = await _context.Users.FindAsync(ApplicationUser.Id);
                    
                    return Page();
                }
            }
        }

        private async Task<bool> UserExists(string id)
        {
            return await _context.Users.AnyAsync(e => e.Id == id);
        }
    }
}
