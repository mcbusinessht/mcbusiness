using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Payments
{
    public class MonCash : PageModel
    {
        private readonly ILogger<MonCash> _logger;

        public MonCash(ILogger<MonCash> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            RedirectToPage("/Users/User/Dashboard");
        }
    }
}
