using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using MCBusiness.Interfaces;

namespace MCBusiness.Pages.Account
{
    public class VerifyEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public VerifyEmailModel( UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender= emailSender;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.EmailConfirmed)
            {
                return RedirectToPage("/Index");
            }
            // Générer un lien de confirmation et renvoyer l'email
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { userId = user.Id, token },
                protocol: Request.Scheme);

            // Ici, envoyez l'email avec votre service d'email (non inclus dans cet exemple)
            await _emailSender.SendEmailAsync(user.Email, "Confirmez votre email", $"Cliquez sur ce lien pour verifer votre email : <a href='{confirmationLink}'>cliquer pour verifier l'email</a>");

            TempData["Message"] = "Un nouvel email de vérification a été envoyé.";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.EmailConfirmed)
            {
                return RedirectToPage("/Index");
            }

            // Générer un lien de confirmation et renvoyer l'email
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { userId = user.Id, token },
                protocol: Request.Scheme);

            // Ici, envoyez l'email avec votre service d'email (non inclus dans cet exemple)
            //Recuperation du fichier  html contenant l'email
                string html_confirm = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "confirm_email.html"));
                //Remplacement des variables
                html_confirm = html_confirm.Replace("{{FullName}}", user.FullName)
                                    .Replace("{{ConfirmationLink}}", confirmationLink);
                 // Sujet de l'email
                string subject = $"Bienvenue chez MC Business Haiti {user.FullName} !";
                try{
                    // Appel de la méthode pour envoyer l'email
                    await _emailSender.SendEmailAsync(user.Email, "Confirmez votre email", html_confirm);

                }catch(Exception ex){
                    Console.WriteLine(ex);
                }
            TempData["Message"] = "Un nouvel email de vérification a été envoyé.";
            return Page();
        }
    }
}
