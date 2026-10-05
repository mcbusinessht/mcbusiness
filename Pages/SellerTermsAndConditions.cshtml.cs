using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages
{
    public class SellerTermsAndConditions : PageModel
    {
        private readonly ILogger<SellerTermsAndConditions> _logger;

        public SellerTermsAndConditions(ILogger<SellerTermsAndConditions> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
