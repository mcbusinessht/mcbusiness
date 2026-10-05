using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Users.Seller
{
    public class Notifications : PageModel
    {
        private readonly ILogger<Notifications> _logger;

        public Notifications(ILogger<Notifications> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
