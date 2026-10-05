using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models; // Assurez-vous d'avoir cette using directive
using System;
using System.Threading.Tasks;

namespace MCBusiness.Pages.Users.Admin.Users
{
    public class LockUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LockUserModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string Id { get; set; }

        public ApplicationUser UserToManage { get; set; }

        public bool IsLocked { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Id))
                return NotFound();

            UserToManage = await _userManager.FindByIdAsync(Id);

            if (UserToManage == null)
                return NotFound();

            IsLocked = await _userManager.IsLockedOutAsync(UserToManage);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(Id);

            if (user == null)
                return NotFound();

            var isLocked = await _userManager.IsLockedOutAsync(user);

            if (isLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null); // Débloquer
                await _userManager.ResetAccessFailedCountAsync(user);
            }
            else
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100)); // Verrouiller pour longtemps
            }

            return RedirectToPage(new { id = Id }); // Recharge la page avec l'état mis à jour
        }
    }
}
