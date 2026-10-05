using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Addresses_Neighborhoods
{
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string NeighborhoodNames { get; set; } = default!;

        [BindProperty]
        public int CityId { get; set; }

        public IActionResult OnGet(int? id)
        {
            if (id.HasValue && id.Value > 0)
            {
                ViewData["CityId"] = new SelectList(_context.Cities.Where(c => c.Id == id), "Id", "Name");
                CityId = id.Value;
            }
            else
            {
                ViewData["CityId"] = new SelectList(_context.Cities, "Id", "Name");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(NeighborhoodNames))
                return Page();

            var names = NeighborhoodNames.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(n => n.Trim())
                                         .Where(n => !string.IsNullOrEmpty(n))
                                         .Distinct();

            foreach (var name in names)
            {
                _context.Neighborhoods.Add(new Neighborhood
                {
                    Name = name,
                    CityId = CityId
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
