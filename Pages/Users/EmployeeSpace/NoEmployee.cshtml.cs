using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Users.EmployeeSpace
{
    public class NoEmployee : PageModel
    {
        private readonly ILogger<NoEmployee> _logger;

        public NoEmployee(ILogger<NoEmployee> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
