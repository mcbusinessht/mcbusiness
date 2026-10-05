using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using MCBusiness.Interfaces;

namespace MCBusiness.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public ForgotPasswordViewModel Input { get; set; }
        public string CustomError{get; set;}
        public string CustomSuccess{get; set;}

        public class ForgotPasswordViewModel
        {
            [Required(ErrorMessage = "L'adresse email est obligatoire.")]
            [EmailAddress(ErrorMessage = "Veuillez entrer une adresse email valide.")]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        
        }
  
        public async Task<IActionResult> OnPostAsync()
        {
            
            if (!ModelState.IsValid)
            {
                
                 Console.WriteLine("le token est"); 
                return Page();
            }
            try{
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if(user== null){
                CustomError="Cet email n'est pas associer a aucun compte MCBusiness .";
                ModelState.AddModelError("CustomErrorKey", CustomError);
                
            }
            if(!await _userManager.IsEmailConfirmedAsync(user)){
                CustomError="Cet email n'est pas verifier, veuillez contacter MCBusiness pour recuperer votre compte .";
                ModelState.AddModelError("CustomErrorKey", CustomError);
                
            }
            if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            {
                // Générer le token de réinitialisation
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                // Générer le lien de réinitialisation
                var resetLink = Url.Page(
                    "/Account/ResetPassword",
                    null,
                    new { token, email = Input.Email },
                    Request.Scheme);

                // Envoyer l'email
                //Recuperation du fichier  html contenant l'email
                string html_reset = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "reset_passw.html"));
                //Remplacement des variables
                html_reset = html_reset.Replace("{{FullName}}", user.FullName)
                                    .Replace("{{ResetLink}}", resetLink);
                try{
                    // Appel de la méthode pour envoyer l'email
                    await _emailSender.SendEmailAsync(user.Email, "Réinitialisation de votre mot de passe", html_reset);

                }catch(Exception ex){
                    Console.WriteLine(ex);
                }
               
                CustomSuccess="Email envoyé avec succès !";
                ModelState.AddModelError("CustomSuccessKey", CustomSuccess);
                TempData["SuccessMessage"] = "Email envoyé avec succès, verifiez votre boite mail!";
                return Page();
            }       
            

        }
        catch (Exception ex)
        {
            // Gérer les erreurs et afficher un message appropri�
            TempData["ErrorMessage"] = $"Erreur lors de l'envoi de l'email : {ex.Message}";
            return Page();
        }

            return Page();
        }
    }
}
