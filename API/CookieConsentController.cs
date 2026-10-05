using Microsoft.AspNetCore.Mvc;
using MCBusiness.Services;
using MCBusiness.Models;
using Microsoft.AspNetCore.Antiforgery;

namespace MCBusiness.API
{
    [ApiController]
    [Route("api/cookie-consent")]
    public class CookieConsentController : ControllerBase
    {
        private readonly ICookieConsentService _cookieConsentService;
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<CookieConsentController> _logger;

        public CookieConsentController(
            ICookieConsentService cookieConsentService,
            IAntiforgery antiforgery,
            ILogger<CookieConsentController> logger)
        {
            _cookieConsentService = cookieConsentService;
            _antiforgery = antiforgery;
            _logger = logger;
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckConsent()
        {
            try
            {
                var hasConsent = await _cookieConsentService.HasValidConsentAsync(HttpContext);
                var settings = await _cookieConsentService.GetUserCookieSettingsAsync(HttpContext);

                return Ok(new
                {
                    hasConsent = hasConsent,
                    settings = new
                    {
                        essential = settings.Essential,
                        functional = settings.Functional,
                        analytics = settings.Analytics,
                        performance = settings.Performance,
                        marketing = settings.Marketing,
                        consentDate = settings.ConsentDate,
                        version = settings.ConsentVersion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification du consentement cookies");
                return Ok(new { hasConsent = false });
            }
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveConsent([FromBody] SaveConsentRequest request)
        {
            try
            {
                // Validation de base
                if (request == null)
                {
                    return BadRequest(new { error = "Données de consentement manquantes" });
                }

                var settings = new CookieSettings
                {
                    Essential = true, // Toujours true
                    Functional = request.Functional,
                    Analytics = request.Analytics,
                    Performance = request.Performance,
                    Marketing = request.Marketing,
                    ConsentDate = DateTimeOffset.UtcNow,
                    ConsentVersion = "1.0"
                };

                await _cookieConsentService.SaveCookieConsentAsync(HttpContext, settings);

                _logger.LogInformation("Consentement cookies sauvegardé via API");

                return Ok(new
                {
                    success = true,
                    message = "Préférences de cookies sauvegardées avec succès",
                    settings = new
                    {
                        essential = settings.Essential,
                        functional = settings.Functional,
                        analytics = settings.Analytics,
                        performance = settings.Performance,
                        marketing = settings.Marketing
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde du consentement cookies");
                return StatusCode(500, new { error = "Erreur interne du serveur" });
            }
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeConsent()
        {
            try
            {
                await _cookieConsentService.InvalidateConsentAsync(HttpContext);

                _logger.LogInformation("Consentement cookies révoqué via API");

                return Ok(new
                {
                    success = true,
                    message = "Consentement révoqué avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la révocation du consentement");
                return StatusCode(500, new { error = "Erreur interne du serveur" });
            }
        }

        [HttpGet("info")]
        public IActionResult GetCookieInfo()
        {
            try
            {
                var cookieInfo = _cookieConsentService.GetCookieInformation();

                var categorizedCookies = cookieInfo
                    .GroupBy(c => c.Category)
                    .ToDictionary(
                        g => g.Key.ToString().ToLower(),
                        g => g.Select(cookie => new
                        {
                            name = cookie.Name,
                            purpose = cookie.Purpose,
                            duration = cookie.Duration,
                            provider = cookie.Provider,
                            isThirdParty = cookie.IsThirdParty,
                            dataProcessing = cookie.DataProcessing
                        }).ToList()
                    );

                return Ok(new
                {
                    cookies = categorizedCookies,
                    lastUpdated = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                    version = "1.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des informations cookies");
                return StatusCode(500, new { error = "Erreur interne du serveur" });
            }
        }
    }

    public class SaveConsentRequest
    {
        public bool Essential { get; set; } = true;
        public bool Functional { get; set; }
        public bool Analytics { get; set; }
        public bool Performance { get; set; }
        public bool Marketing { get; set; }
    }
}
