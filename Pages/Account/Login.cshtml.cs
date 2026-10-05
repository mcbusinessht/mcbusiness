using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MCBusiness.Models;
using MCBusiness.Interfaces;
using MCBusiness.Services;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Pages.Account
{
    [ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ITwoFactorSendThrottle _twoFactorSendThrottle;
        private readonly ReCaptchaSettings _reCaptchaSettings;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            IEmailSender emailSender,
            ITwoFactorSendThrottle twoFactorSendThrottle,
            IOptions<ReCaptchaSettings> reCaptchaOptions)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _twoFactorSendThrottle = twoFactorSendThrottle;
            _reCaptchaSettings = reCaptchaOptions.Value;
        }

        [BindProperty]
        public LoginInputModel Input { get; set; }

        [BindProperty]
        public string ReturnUrl { get; set; }
        public string ReCaptchaSiteKey => _reCaptchaSettings.SiteKey;

        public class LoginInputModel
        {
            [Required] public string Username { get; set; }
            [Required] public string Password { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            PreventLoginPageCaching();
            ReturnUrl = GetUsableReturnUrl(returnUrl);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            PreventLoginPageCaching();

            if (!ModelState.IsValid)
                return Page();

            var user = await FindUserAsync(Input.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "Identifiants incorrects");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                isPersistent: false,
                lockoutOnFailure: true
            );

            if (result.RequiresTwoFactor)
            {
                var returnUrl = GetUsableReturnUrl(ReturnUrl);
                var sendAttempt = await _twoFactorSendThrottle.TryAcquireAsync(user);
                if (sendAttempt.WasAcquired)
                {
                    try
                    {
                // 🔐 GÉNÉRER + ENVOYER LE CODE EMAIL
                var code = await _userManager.GenerateTwoFactorTokenAsync(
                    user,
                    TokenOptions.DefaultEmailProvider
                );

                var html = await LoadEmailTemplateAsync("auth_2FA.html");
                html = html.Replace("{{FullName}}", user.FullName)
                        .Replace("{{VerificationCode}}", code);

                await _emailSender.SendEmailAsync(
                    user.Email,
                    "Code de vérification",
                    html
                );

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur lors de l'envoi du code 2FA pour {UserId}", user.Id);
                        TempData["TwoFactorError"] =
                            "Le code n'a pas pu etre envoye. Vous pourrez reessayer depuis la page de verification.";
                    }
                }

                return RedirectToPage("./TwoFactor", new { returnUrl });
            }

            if (result.Succeeded)
            {
                // Envoyer la notification de connexion
                await SendLoginNotificationAsync(user);
                return await RedirectByRoleAsync(user, ReturnUrl);
            }

            ModelState.AddModelError("", "Identifiants incorrects");
            return Page();
        }

        private async Task<ApplicationUser> FindUserAsync(string input)
        {
            if (input.Contains("@"))
                return await _userManager.FindByEmailAsync(input);

            if (input.All(char.IsDigit) || input.StartsWith("+"))
                return await _userManager.Users.FirstOrDefaultAsync(u =>
                    u.PhoneNumber == input || u.PhoneNumber.EndsWith(input));

            return await _userManager.FindByNameAsync(input);
        }

        private async Task SendTwoFactorCodeAsync(ApplicationUser user)
        {
            var code = await _userManager.GenerateTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultEmailProvider);

            var html = await LoadEmailTemplateAsync("auth_2FA.html");
            html = html.Replace("{{FullName}}", user.FullName)
                       .Replace("{{VerificationCode}}", code);

            await _emailSender.SendEmailAsync(
                user.Email, "Code de vérification", html);
        }

        private async Task<IActionResult> RedirectByRoleAsync(ApplicationUser user, string returnUrl)
        {
            var usableReturnUrl = GetUsableReturnUrl(returnUrl);
            if (!string.IsNullOrWhiteSpace(usableReturnUrl))
            {
                return LocalRedirect(usableReturnUrl);
            }

            if (await _userManager.IsInRoleAsync(user, "Admin") ||
                await _userManager.IsInRoleAsync(user, "SecondAdmin"))
                return RedirectToPage("/Users/Admin/Dashboard");

            if (await _userManager.IsInRoleAsync(user, "Seller"))
                return RedirectToPage("/Users/Seller/Dashboard");

            return RedirectToPage("/Users/User/Dashboard");
        }

        private string GetUsableReturnUrl(string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl == Url.Content("~/") || returnUrl == "/")
            {
                return null;
            }

            if (!Url.IsLocalUrl(returnUrl))
            {
                return null;
            }

            var normalized = returnUrl.Split('?')[0].TrimEnd('/').ToLowerInvariant();
            if (normalized == "/account/login" ||
                normalized == "/account/twofactor" ||
                normalized == "/account/signout")
            {
                return null;
            }

            return returnUrl;
        }

        private async Task<string> LoadEmailTemplateAsync(string file)
        {
            return await System.IO.File.ReadAllTextAsync(
                Path.Combine("wwwroot", "emails", file));
        }

        private async Task SendLoginNotificationAsync(ApplicationUser user)
        {
            try
            {
                var userAgent = Request.Headers["User-Agent"].ToString();
                var detector = new DeviceDetectorNET.DeviceDetector(userAgent);
                detector.Parse();

                var deviceType = detector.IsMobile() ? "Mobile" :
                                 detector.IsDesktop() ? "Desktop" :
                                 detector.IsTablet() ? "Tablette" : "Inconnu";

                var deviceBrand = detector.GetBrandName() ?? "Inconnu";
                var deviceModel = detector.GetModel() ?? "";
                var browser = detector.GetClient().Match?.Name ?? "Inconnu";

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Inconnue";

                var html = await LoadEmailTemplateAsync("login_notification.html");
                html = html.Replace("{{FullName}}", user.FullName)
                           .Replace("{{DeviceType}}", deviceType)
                           .Replace("{{DeviceBrand}}", deviceBrand)
                           .Replace("{{DeviceModel}}", deviceModel)
                           .Replace("{{Browser}}", browser)
                           .Replace("{{ipAddress}}", ip)
                           .Replace("{{DateTime}}", DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm"));

                await _emailSender.SendEmailAsync(
                    user.Email,
                    "Nouvelle connexion détectée",
                    html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de la notification de connexion pour {Email}", user.Email);
                // Ne pas bloquer la connexion si l'email échoue
            }
        }

        public async Task<IActionResult> OnGetLogOutAsync()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session?.Clear();
            PreventLoginPageCaching();
            return RedirectToPage("/Account/Login");
        }

        private void PreventLoginPageCaching()
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }
    }
}
