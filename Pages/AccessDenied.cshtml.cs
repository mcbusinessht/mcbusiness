using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Services;

namespace MCBusiness.Pages
{
    public class AccessDenied : PageModel
    {
        private readonly ILogger<AccessDenied> _logger;

        public AccessDenied(ILogger<AccessDenied> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            return Page();
        }
    }
}
