using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Pages.Users.Admin.Medias
{
    public class Index : PageModel
    {
        private readonly ILogger<Index> _logger;
        private readonly AppDbContext _context; 

        public Index(ILogger<Index> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        [BindProperty]
        public List <SystemMedia> media{get; set;}= new List<SystemMedia>();

        public void OnGet()
        {
            media=_context.SystemMedias.ToList();
        }
    }
}
