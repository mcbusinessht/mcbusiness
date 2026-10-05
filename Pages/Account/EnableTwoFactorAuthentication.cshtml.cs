using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Models; 

namespace MCBusiness.Pages.Account
{
    [Authorize]
    public class EnableTwoFactorAuthenticationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public EnableTwoFactorAuthenticationModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager; 
        }

        public bool IsTwoFactorEnabled { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostEnableAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["TwoFactorError"] =
                    "Confirmez votre adresse email avant d'activer l'authentification a deux facteurs.";
                return RedirectToPage("./EnableTwoFactorAuthentication");
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
            if (!result.Succeeded)
            {
                TempData["TwoFactorError"] = "Impossible d'activer l'authentification a deux facteurs.";
            }

            return RedirectToPage("./EnableTwoFactorAuthentication");
        }

        public async Task<IActionResult> OnPostDisableAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!result.Succeeded)
            {
                TempData["TwoFactorError"] = "Impossible de desactiver l'authentification a deux facteurs.";
            }

            return RedirectToPage("./EnableTwoFactorAuthentication");
        }
    }
}
