using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Sellers_CompanySectors
{
    public class EditModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public EditModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public CompanySector CompanySector { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var companysector =  await _context.CompanySectors.FirstOrDefaultAsync(m => m.Id == id);
            if (companysector == null)
            {
                return NotFound();
            }
            CompanySector = companysector;
           ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
           ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Email");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(CompanySector).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CompanySectorExists(CompanySector.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool CompanySectorExists(int id)
        {
            return _context.CompanySectors.Any(e => e.Id == id);
        }
    }
}
