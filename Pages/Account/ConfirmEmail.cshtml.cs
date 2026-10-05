using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models; // Remplacez par votre espace de noms pour les modèles

namespace MCBusiness.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                StatusMessage = "Paramêtres de confirmation invalides.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = "Utilisateur introuvable.";
                return Page();
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                StatusMessage = "Votre adresse email a été confirmée avec succès.";
                return RedirectToPage("/Users/User/Dashboard"); // Redirige après succès, ou afficher un message de succès
            }

            StatusMessage = "échec de la confirmation de l'email.";
            return Page();
        }
    }
}
