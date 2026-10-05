using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;


namespace MCBusiness.Pages.Users.Admin.Products
{
    public class ChooseModel : PageModel
    {
        private readonly ILogger<ChooseModel> _logger;
        private readonly AppDbContext _context;
        public ChooseModel(ILogger<ChooseModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        [BindProperty] 
        public  Company Company{ get; set; }
        public void OnGet()
        {
            ViewData["Company"] = new SelectList(_context.Companies, "Id", "Name");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ViewData["Company"] = new SelectList(_context.Companies, "Id", "Name");

            return RedirectToPage("/Users/Admin/Products/Create", null, new { id = Company.Id });
        }
    }
}
