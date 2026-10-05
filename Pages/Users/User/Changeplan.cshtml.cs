using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
namespace MCBusiness.Pages.Users.User
{
    public class ChangeplanModel : PageModel
    {
        private readonly ILogger<ChangeplanModel> _logger;
        private readonly AppDbContext _context;

        public ChangeplanModel(ILogger<ChangeplanModel> logger ,AppDbContext context)
        {
            _logger = logger;
            _context= context;
        }
        public List <AmbassadorPlan> plans=new List<AmbassadorPlan>();
        public void OnGet()
        {
            plans=_context.AmbassadorPlans.ToList();
        }
    }
}
