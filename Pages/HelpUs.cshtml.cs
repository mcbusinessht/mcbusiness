using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages
{
    public class HelpUs : PageModel
    {
        private readonly ILogger<HelpUs> _logger;

        public HelpUs(ILogger<HelpUs> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
