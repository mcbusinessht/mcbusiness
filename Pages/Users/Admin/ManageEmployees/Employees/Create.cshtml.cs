using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_ManageEmployees_Employees
{
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["CurrencyBase"] = new SelectList(_context.Currencies, "Id", "CurrencyBase");
        ViewData["PostId"] = new SelectList(_context.Posts, "Id", "Name");
        ViewData["UserId"] = new SelectList(_context.Users.OrderBy(u => u.UserName), "Id", "UserName");
            return Page();
        }

        [BindProperty]
        public Employee Employee { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            
            // Convertir tous les DateTimeOffset en UTC pour PostgreSQL
            Employee.HiredAt = Employee.HiredAt.ToUniversalTime();
            Employee.UpdatedAt = Employee.UpdatedAt?.ToUniversalTime();
            
            if (Employee.ContractEndDate.HasValue)
            {
                Employee.ContractEndDate = Employee.ContractEndDate.Value.ToUniversalTime();
            }

            _context.Employees.Add(Employee);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
