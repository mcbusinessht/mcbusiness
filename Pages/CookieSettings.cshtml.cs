using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages
{
    public class CookieSettingsModel : PageModel
    {
        private readonly ICookieConsentService _cookieConsentService;
        private readonly ILogger<CookieSettingsModel> _logger;

        [BindProperty]
        public CookieSettings Settings { get; set; } = new();
        
        public CookieSettings CurrentSettings { get; set; } = new();

        public CookieSettingsModel(
            ICookieConsentService cookieConsentService,
            ILogger<CookieSettingsModel> logger)
        {
            _cookieConsentService = cookieConsentService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                CurrentSettings = await _cookieConsentService.GetUserCookieSettingsAsync(HttpContext);
                Settings = new CookieSettings
                {
                    Essential = CurrentSettings.Essential,
                    Functional = CurrentSettings.Functional,
                    Analytics = CurrentSettings.Analytics,
                    Marketing = CurrentSettings.Marketing,
                    Performance = CurrentSettings.Performance,
                    ConsentVersion = "1.0"
                };

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des paramètres cookies");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    CurrentSettings = await _cookieConsentService.GetUserCookieSettingsAsync(HttpContext);
                    return Page();
                }

                // Les cookies essentiels sont toujours activ�s
                Settings.Essential = true;

                await _cookieConsentService.SaveCookieConsentAsync(HttpContext, Settings);

                _logger.LogInformation("Paramêtres cookies mis à jour avec succès");

                // Rediriger avec un paramêtre de succès
                return RedirectToPage("/CookieSettings", new { success = "true" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde des paramètres cookies");
                ModelState.AddModelError("", "Une erreur s'est produite lors de la sauvegarde de vos préférences.");
                
                CurrentSettings = await _cookieConsentService.GetUserCookieSettingsAsync(HttpContext);
                return Page();
            }
        }
    }
}
