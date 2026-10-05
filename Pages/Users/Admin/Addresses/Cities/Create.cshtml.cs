using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Addresses_Cities
{
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string CityNames { get; set; } = default!;

        [BindProperty]
        public int StateId { get; set; }

        public IActionResult OnGet(int? id)
        {
            if (id.HasValue && id.Value > 0)
            {
                ViewData["StateId"] = new SelectList(_context.States.Where(s => s.Id == id), "Id", "Name");
                StateId = id.Value;
            }
            else
            {
                ViewData["StateId"] = new SelectList(_context.States, "Id", "Name");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(CityNames))
                return Page();

            var cityNames = CityNames.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(n => n.Trim())
                                     .Where(n => !string.IsNullOrEmpty(n))
                                     .Distinct();

            foreach (var name in cityNames)
            {
                _context.Cities.Add(new City
                {
                    Name = name,
                    StateId = StateId
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
