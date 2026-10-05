using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using System;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Interfaces;
using Sprache;
using System.IO;
using MCBusiness.Services;
using Microsoft.Extensions.Options;

namespace MCBusiness.Pages.Account
{
    [ValidateAntiForgeryToken]
    public class SignUpModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<SignUpModel> _logger;
        private readonly IReCaptchaService _reCaptchaService;
        private readonly ReCaptchaSettings _reCaptchaSettings;
        private readonly INotificationService _notificationService;
        private readonly PointTransactionService _pointTransactionService;
        
        public SignUpModel(ILogger<SignUpModel> logger, UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, AppDbContext context, 
            RoleManager<IdentityRole> roleManager, IEmailSender emailSender,
            IReCaptchaService reCaptchaService, IOptions<ReCaptchaSettings> reCaptchaSettings, 
            INotificationService notificationService, PointTransactionService pointTransactionService)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _reCaptchaService = reCaptchaService;
            _reCaptchaSettings = reCaptchaSettings.Value;
            _notificationService = notificationService;
            _pointTransactionService = pointTransactionService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string UserNameError { get; set; }
        public string PasswordError { get; set; }
        public string CustomError1 { get; set; }
        public string ErrorEmail { get; set; }
        public string ErrorCodeParrainage { get; set; }
        public string ReCaptchaSiteKey => _reCaptchaSettings.SiteKey;


        public class InputModel
        {
            [Required]
            public string FirstName { get; set; }

            [Required]
            public string LastName { get; set; }

            [MinLength(8, ErrorMessage = "Le Nom d'utilisateur doit contenir au moins 8 caractères.")]
            public string UserName { get; set; }

            public string Gender { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [RegularExpression(@"^\d{8,}$", ErrorMessage = "Numero Incorrecte.")]
            public string PhoneNumber { get; set; }
            public string CodeNumber { get; set; }

            public string ReferralCode { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
            public string ConfirmPassword { get; set; }
        }

        public string ReferralCode { get; private set; }


        public void OnGet()
        {
            ReferralCode = Request.Query["referralCode"];
            var referralCodeStocker = HttpContext.Session.GetString("referralcode");
            if (!string.IsNullOrEmpty(ReferralCode))
            {
                Input = new InputModel
                {
                    ReferralCode = ReferralCode
                };
                HttpContext.Session.SetString("referralcode", ReferralCode);
            }
            else if (!string.IsNullOrEmpty(referralCodeStocker))
            {
                Input = new InputModel
                {
                    ReferralCode = referralCodeStocker
                };
            }

        }


        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Vérification reCAPTCHA avec le nouveau service
            // var reCaptchaToken = Request.Form["g-recaptcha-response"];
            // if (!await _reCaptchaService.VerifyAsync(reCaptchaToken))
            // {
            //     ModelState.AddModelError(string.Empty, "La vérification reCAPTCHA a échoué. Veuillez réessayer.");
            //     return Page();
            // }                

            // Générer un nom d'utilisateur automatiquement si non fourni
            if (string.IsNullOrWhiteSpace(Input.UserName))
            {
                Input.UserName = await GenerateUsernameFromEmail(Input.Email);
            }
            else
            {
                // Vérification du nom d'utilisateur seulement s'il est fourni
                var userExists = await _userManager.Users.AnyAsync(u => u.NormalizedUserName == Input.UserName.ToUpper());

                if (userExists)
                {
                    CustomError1 = "Ce nom d'utilisateur est déjà pris. Veuillez en choisir un autre.";
                    ModelState.AddModelError("CustomErrorKey1", CustomError1);
                    ModelState.AddModelError(string.Empty, "Ce nom d'utilisateur est déjà pris. Veuillez en choisir un autre.");
                    return Page();
                }
            }

            // Validation du mot de passe
            if (!IsPasswordStrong(Input.Password))
            {
                ModelState.AddModelError(string.Empty, "Le mot de passe doit contenir au moins 8 caractéres, une majuscule, une minuscule et un chiffre.");
                return Page();
            }
            // Vérification email unique
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == Input.Email.ToLower()))
            {
                ErrorEmail = "Cet email est deja associer a un compte MCBusiness. Veuillez en choisir un autre.";
                ModelState.AddModelError("ErrorEmailKey", ErrorEmail);
                return Page();
            }

            // Vérification téléphone unique
            var fullPhoneNumber = Input.CodeNumber + Input.PhoneNumber;
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == fullPhoneNumber))
            {
                ModelState.AddModelError(string.Empty, "Ce numéro de téléphone est déjà associé à un compte MCBusiness. Veuillez en choisir un autre.");
                return Page();
            }
            if (Input.ReferralCode != null)
            {
                if (!await _context.Users.AnyAsync(u => u.ReferralCode == Input.ReferralCode))
                {
                    ErrorCodeParrainage = "Ce code parrainage est invalide, veuillez contacter votre parrain.";
                    ModelState.AddModelError("ErrorCodeParrainageKey", ErrorCodeParrainage);
                    return Page();
                }
            }

            var hasCurrency = await _context.Currencies.AnyAsync();
            if (!hasCurrency)
            {
                // Si aucune devise n'existe, créer une devise par défaut
                var defaultCurrency = new Currency
                {
                    Code = "HTG",
                    Symbol = "HTG",
                    RateToBase = 1.0m,
                    IsDefault = true
                };
                _context.Currencies.Add(defaultCurrency);
                await _context.SaveChangesAsync();
            }
            // Créer un utilisateur
            var user = new ApplicationUser
            {
                UserName = Input.UserName,
                Email = Input.Email,
                FirstName = Input.FirstName,
                LastName = Input.LastName,
                Gender = string.IsNullOrWhiteSpace(Input.Gender) ? null : Input.Gender,
                PhoneNumber = Input.CodeNumber + Input.PhoneNumber,
                ReferralCode = GenerateReferralCode(),
                ReferredBy = string.IsNullOrWhiteSpace(Input.ReferralCode) ? null : Input.ReferralCode.Trim(),
            };


            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {


                // Vérifier si le rôle "User" existe et le créer si nécessaire
                if (!await _roleManager.RoleExistsAsync("User"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                }

                // Ajouter l'utilisateur au rôle "User"
                await _userManager.AddToRoleAsync(user, "User");

                // Rôle Admin supprimé - seul le rôle User est attribué

                

                // Créer des enregistrements associés
                await CreateAssociatedData(user);

                // Attribuer 50 points bonus d'inscription
                await _pointTransactionService.AwardPointsAsync(
                    user.Id,
                    50,
                    PointTransactionType.WelcomeBonus,
                    "Bonus d'inscription - Bienvenue chez MCBusiness !",
                    "Points bonus automatiques pour nouveaux membres"
                );

                //Connexion de l'utilisateur
                // await _signInManager.SignInAsync(user, isPersistent: false);

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Page("/Account/ConfirmEmail", null, new { userId = user.Id, token = token }, Request.Scheme);
                HttpContext.Session.SetString("confirmationLink", confirmationLink);

                //Recuperation du fichier  html contenat l'email
                string html_welcome = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "welcome_user.html"));
                string html_confirm = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "confirm_email.html"));
                //Remplacement des variables
                html_welcome = html_welcome.Replace("{{FullName}}", user.FullName);
                html_confirm = html_confirm.Replace("{{FullName}}", user.FullName)
                                    .Replace("{{ConfirmationLink}}", confirmationLink);
                // Sujet de l'email
                string subject = $"Bienvenue chez MC Business Haiti {user.FullName} !";
                try
                {
                    var not = new Notification
                    {
                        UserId = user.Id,
                        Message = $"Bienvenue chez MCBusiness {user.FullName}, nous vous remercions d'avoir choisi de vous inscrire sur notre plateforme MC Business.",
                    };
                    // await _notificationService.NotifyAsync(not);
                    await _context.Notifications.AddAsync(not);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Notification de bienvenue envoye {userId}", user.Id);

                    // Envoi de l'email de bienvenue
                    _logger.LogInformation("Envoi email à {email}", user.Email);
                    // Appel de la méthode pour envoyer l'email
                    await _emailSender.SendEmailAsync(user.Email, subject, html_welcome);
                    await _emailSender.SendEmailAsync(user.Email, "Confirmez votre email", html_confirm);

                    _logger.LogInformation("Email de bienvenue envoyé");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    _logger.LogError("Erreur lors de l'envoi de l'email de bienvenue : {error}", ex.Message);
                }

                return RedirectToPage("/Account/WaitingConfirmation", new { email = user.Email });
            }


            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private bool IsPasswordStrong(string password)
        {
            return password.Length >= 8 && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
        }

        private string GenerateReferralCode()
        {
            var code = Guid.NewGuid().ToString("N").Substring(0, 8); // Générer un code unique
            var flag = _context.Users.FirstOrDefault(r => r.ReferralCode == code);
            while (flag != null)
            {
                code = Guid.NewGuid().ToString("N").Substring(0, 8);
                flag = _context.Users.FirstOrDefault(r => r.ReferralCode == code);

            }

            return code;
        }

        private async Task CreateAssociatedData(ApplicationUser user)
        {
            
                // Créer un portefeuille pour l'utilisateur
                var wallet = new Wallet
                {
                    UserId = user.Id,
                    Type = WalletType.Customer,
                    Balance = 0 // Solde initial
                };
                _context.Wallets.Add(wallet);

                // Créer un Panier pour l'utilisateur
                var cart = new Cart
                {
                    UserId = user.Id,
                };
                _context.Carts.Add(cart);

                var newsletter = new Newsletter
                {
                    Email = user.Email
                };

                _context.Newsletters.Add(newsletter);


                // NOUVEAU SYSTÈME: Les cookies/sessions persistent automatiquement après inscription
                // On ne crée PLUS d'entrées dans Referrals table
                // Les commissions seront attribuées lors de l'achat via les cookies ambassador_ref
                
                // Simplement notifier l'ambassadeur si un code de parrainage est détecté
                if (!string.IsNullOrEmpty(user.ReferredBy))
                {
                    var referrer = await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == user.ReferredBy);
                    if (referrer != null)
                    {
                        // Notifier l'ambassadeur de la nouvelle inscription
                        var notification = new Notification
                        {
                            UserId = referrer.Id,
                            Message = $"Nouvel utilisateur inscrit via votre lien: '{user.UserName}'. Commission à venir lors de son premier achat!",
                        };
                        await _notificationService.NotifyAsync(notification);
                        
                        _logger.LogInformation($"Inscription via ambassadeur {referrer.Id} - Cookies persistent pour 7 jours");
                    }
                }
                await _context.SaveChangesAsync();
            
        }

        private async Task<string> GenerateUsernameFromEmail(string email)
        {
            var baseUsername = email.Split('@')[0].ToLower();
            
            // S'assurer que le username fait au moins 8 caractères
            if (baseUsername.Length < 8)
            {
                baseUsername = baseUsername.PadRight(8, '0');
            }
            
            var username = baseUsername;
            var counter = 1;

            while (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == username.ToUpper()))
            {
                username = $"{baseUsername}{counter}";
                counter++;
            }

            return username;
        }
    }
}
