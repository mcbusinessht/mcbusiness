using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.Admin.Products
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public CreateModel(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public IActionResult OnGet(int? id)
        {
            var Company = _context.Companies.FirstOrDefault(c => c.Id == id);
            var parentCategories = _context.Categories.FirstOrDefault(c => c.Id == Company.Sector.Id);
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.ParentCategoryId == parentCategories.Id), "Id", "Name");
            ViewData["CompanyId"] = new SelectList(_context.Companies.Where(c => c.Id == Company.Id), "Id", "Name");
            return Page();
        }

        [BindProperty]
        public Product Product { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                _context.Products.Add(Product);
                await _context.SaveChangesAsync();

                return RedirectToPage("./ProductVariants/Create", null, new { id = Product.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erreur lors de l'enregistrement: " + ex.Message);
                return Page();
            }
        }
    }
}
