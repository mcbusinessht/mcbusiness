using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages
{
    public class TermsAndConditions : PageModel
    {
        private readonly ILogger<TermsAndConditions> _logger;

        public TermsAndConditions(ILogger<TermsAndConditions> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
