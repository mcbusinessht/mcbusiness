using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.SellerApplication
{
    public class SendSuccessfully : PageModel
    {
        private readonly ILogger<SendSuccessfully> _logger;

        public SendSuccessfully(ILogger<SendSuccessfully> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}