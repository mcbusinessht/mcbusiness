using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Account
{
    public class ResetPasswordConfirmation : PageModel
    {
        private readonly ILogger<ResetPasswordConfirmation> _logger;

        public ResetPasswordConfirmation(ILogger<ResetPasswordConfirmation> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
