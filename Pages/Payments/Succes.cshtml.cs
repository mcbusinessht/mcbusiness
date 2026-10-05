using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Payments
{
    public class Succes : PageModel
    {
        private readonly ILogger<Succes> _logger;

        public Succes(ILogger<Succes> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
