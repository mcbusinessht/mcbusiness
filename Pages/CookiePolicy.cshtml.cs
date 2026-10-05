using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages
{
    public class CookiePolicyModel : PageModel
    {
        private readonly ICookieConsentService _cookieConsentService;

        public List<CookieInfo> CookieInformation { get; set; } = new();

        public CookiePolicyModel(ICookieConsentService cookieConsentService)
        {
            _cookieConsentService = cookieConsentService;
        }

        public void OnGet()
        {
            CookieInformation = _cookieConsentService.GetCookieInformation();
        }
    }
}
