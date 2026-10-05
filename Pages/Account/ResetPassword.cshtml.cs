using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models; 

namespace MCBusiness.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string PasswordSuccess { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public IActionResult OnGet()
        {
            // Initialisation de l'objet Input
            Input = new InputModel();

            // Récupérer les valeurs de la requête
            Input.Email = Request.Query["email"];
            Input.Token = Request.Query["token"];

            if (string.IsNullOrEmpty(Input.Email) || string.IsNullOrEmpty(Input.Token))
            {
                return BadRequest("Le token ou l'email est manquant.");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("échec de la validation du modèle.");
                return Page();
            }

            Console.WriteLine($"Email reçu : {Input.Email}");
            Console.WriteLine($"Token reçu : {Input.Token}");

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                Console.WriteLine("Utilisateur introuvable.");
                return Page();
            }
            if (!IsPasswordStrong(Input.Password))
            {
                ModelState.AddModelError(string.Empty, "Le mot de passe doit contenir au moins 8 caractères, une majuscule, une minuscule et un chiffre.");
                return Page();
            }
            var result = await _userManager.ResetPasswordAsync(user, Input.Token, Input.Password);
            if (result.Succeeded)
            {
                Console.WriteLine("Réinitialisation réussie.");
                StatusMessage = "Votre mot de passe a été réinitialisé avec succès.";
                PasswordSuccess="Felicitations, Votre mot de passe a ete reinitialiser avec success";
                ModelState.AddModelError("PasswordSuccessKey",PasswordSuccess);
                return Page();
            }

            foreach (var error in result.Errors)
            {
                Console.WriteLine($"Erreur : {error.Description}");
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
         private bool IsPasswordStrong(string password)
        {
            return password.Length >= 8 && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
        }
}

    public class InputModel
    {
        [Required(ErrorMessage = "Le token est obligatoire.")]
        public string Token { get; set; }

        [Required(ErrorMessage = "L'adresse email est obligatoire.")]
        [EmailAddress(ErrorMessage = "Veuillez entrer une adresse email valide.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères, une majuscule, une minuscule et un chiffre.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Veuillez confirmer votre mot de passe.")] 
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmPassword { get; set; }
    }
     
}


