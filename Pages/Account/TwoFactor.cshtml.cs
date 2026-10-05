using System.ComponentModel.DataAnnotations;
using DeviceDetectorNET;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Interfaces;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages.Account
{
    public class TwoFactorModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ITwoFactorSendThrottle _twoFactorSendThrottle;
        private readonly ILogger<TwoFactorModel> _logger;

        public TwoFactorModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ITwoFactorSendThrottle twoFactorSendThrottle,
            ILogger<TwoFactorModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _twoFactorSendThrottle = twoFactorSendThrottle;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Le code de verification est requis.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Le code doit contenir exactement 6 chiffres.")]
        public string Code { get; set; }

        public bool CanResend { get; set; } = true;
        public int RetryAfterSeconds { get; set; }
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            ReturnUrl = GetUsableReturnUrl(ReturnUrl);
            ErrorMessage = TempData["TwoFactorError"] as string;
            SuccessMessage = TempData["TwoFactorSuccess"] as string;
            await LoadResendStatusAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            if (!ModelState.IsValid)
            {
                await LoadResendStatusAsync(user);
                return Page();
            }

            var result = await _signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultEmailProvider,
                Code.Trim(),
                isPersistent: false,
                rememberClient: false);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    "",
                    "Compte temporairement verrouille apres plusieurs codes invalides.");
                await LoadResendStatusAsync(user);
                return Page();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Code invalide.");
                await LoadResendStatusAsync(user);
                return Page();
            }

            await _twoFactorSendThrottle.ClearAsync(user);
            await SendLoginNotificationAsync(user);
            return await RedirectByRoleAsync(user, ReturnUrl);
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var sendAttempt = await _twoFactorSendThrottle.TryAcquireAsync(user);
            if (!sendAttempt.WasAcquired)
            {
                TempData["TwoFactorError"] =
                    $"Veuillez attendre {sendAttempt.RetryAfterSeconds} seconde(s) avant un nouvel envoi.";
                return RedirectToPage("/Account/TwoFactor", new { returnUrl = ReturnUrl });
            }

            try
            {
                await SendTwoFactorCodeAsync(user);
                TempData["TwoFactorSuccess"] = "Le code de verification a ete renvoye.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du renvoi du code 2FA pour {UserId}", user.Id);
                TempData["TwoFactorError"] =
                    "Le code n'a pas pu etre envoye. Verifiez votre adresse email puis reessayez.";
            }

            return RedirectToPage("/Account/TwoFactor", new { returnUrl = ReturnUrl });
        }

        private async Task SendTwoFactorCodeAsync(ApplicationUser user)
        {
            var code = await _userManager.GenerateTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultEmailProvider);

            var html = await System.IO.File.ReadAllTextAsync(
                Path.Combine("wwwroot", "emails", "auth_2FA.html"));

            html = html.Replace("{{FullName}}", user.FullName)
                .Replace("{{VerificationCode}}", code);

            await _emailSender.SendEmailAsync(
                user.Email,
                "Code de verification",
                html);
        }

        private async Task LoadResendStatusAsync(ApplicationUser user)
        {
            var status = await _twoFactorSendThrottle.GetStatusAsync(user);
            CanResend = status.IsAllowed;
            RetryAfterSeconds = status.RetryAfterSeconds;
        }

        private async Task SendLoginNotificationAsync(ApplicationUser user)
        {
            try
            {
                var userAgent = Request.Headers["User-Agent"].ToString();
                var detector = new DeviceDetector(userAgent);
                detector.Parse();

                var deviceType = detector.IsMobile() ? "Mobile" :
                    detector.IsDesktop() ? "Desktop" :
                    detector.IsTablet() ? "Tablette" : "Inconnu";

                var deviceBrand = detector.GetBrandName() ?? "Inconnu";
                var deviceModel = detector.GetModel() ?? "";
                var browser = detector.GetClient().Match?.Name ?? "Inconnu";
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Inconnue";

                var html = await System.IO.File.ReadAllTextAsync(
                    Path.Combine("wwwroot", "emails", "login_notification.html"));

                html = html.Replace("{{FullName}}", user.FullName)
                    .Replace("{{DeviceType}}", deviceType)
                    .Replace("{{DeviceBrand}}", deviceBrand)
                    .Replace("{{DeviceModel}}", deviceModel)
                    .Replace("{{Browser}}", browser)
                    .Replace("{{ipAddress}}", ip)
                    .Replace("{{DateTime}}", DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm"));

                await _emailSender.SendEmailAsync(
                    user.Email,
                    "Nouvelle connexion detectee",
                    html);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de l'envoi de la notification de connexion pour {UserId}",
                    user.Id);
            }
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
            {
                return RedirectToPage("/Users/Admin/Dashboard");
            }

            if (await _userManager.IsInRoleAsync(user, "Seller"))
            {
                return RedirectToPage("/Users/Seller/Dashboard");
            }

            return RedirectToPage("/Users/User/Dashboard");
        }

        private string GetUsableReturnUrl(string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl == "/" || returnUrl == Url.Content("~/"))
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
    }
}
