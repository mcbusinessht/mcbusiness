using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Addresses_Countries
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public List<Country> Countries { get; set; } = new()
        {
            new Country() // pr�rempli un pour démarrer
        };

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            foreach (var country in Countries)
            {
                if (!string.IsNullOrWhiteSpace(country.Name) &&
                    !string.IsNullOrWhiteSpace(country.CountryCode))
                {
                    _context.Countries.Add(country);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
