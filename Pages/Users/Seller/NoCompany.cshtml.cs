using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Users.Seller
{
    public class NoCompany : PageModel
    {
        private readonly ILogger<NoCompany> _logger;

        public NoCompany(ILogger<NoCompany> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
