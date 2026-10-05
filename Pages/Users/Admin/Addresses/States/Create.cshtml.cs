using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Addresses_States
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public int CountryId { get; set; }

        [BindProperty]
        public string CityInput { get; set; } = string.Empty;

        public SelectList CountrySelectList { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var countries = _context.Countries.ToList();
            if (id.HasValue && id.Value > 0)
                CountrySelectList = new SelectList(countries, "Id", "Name", id.Value);
            else
                CountrySelectList = new SelectList(countries, "Id", "Name");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (CountryId == 0 || string.IsNullOrWhiteSpace(CityInput))
            {
                ModelState.AddModelError(string.Empty, "Tous les champs sont requis.");
                CountrySelectList = new SelectList(_context.Countries.ToList(), "Id", "Name");
                return Page();
            }

            var country = await _context.Countries.FindAsync(CountryId);
            if (country == null)
            {
                ModelState.AddModelError(string.Empty, "Pays introuvable.");
                CountrySelectList = new SelectList(_context.Countries.ToList(), "Id", "Name");
                return Page();
            }

            var cityNames = CityInput
                .Split(',', '\n', ';')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            var existingStateNames = _context.States
                .Where(s => s.CountryId == CountryId)
                .Select(s => s.Name)
                .ToHashSet();

            foreach (var name in cityNames)
            {
                if (!existingStateNames.Contains(name))
                {
                    _context.States.Add(new State
                    {
                        Name = name,
                        CountryId = CountryId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index", new { id = CountryId });
        }
    }
}
