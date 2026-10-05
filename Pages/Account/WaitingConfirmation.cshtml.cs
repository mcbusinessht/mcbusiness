using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;


namespace MCBusiness.Pages.Account
{
    public class WaitingConfirmation : PageModel
    {
        private readonly ILogger<WaitingConfirmation> _logger;

        public WaitingConfirmation(ILogger<WaitingConfirmation> logger)
        {
            _logger = logger;
        }
        [BindProperty]
        public string Email{get; set; }
        public string Link{get; set;}
        public void OnGet(string email)
        {
            Email = email;
            Link = HttpContext.Session.GetString("confirmationLink");
;
        }
    }
}
