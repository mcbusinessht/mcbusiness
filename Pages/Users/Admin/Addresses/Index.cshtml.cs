using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Admin.Addresses
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Country> Countries { get; set; }
        public List<State> States { get; set; }
        public List<City> Cities { get; set; }
        public List<Neighborhood> Neighborhoods { get; set; }

        public async Task OnGetAsync()
        {
            Countries = await _context.Countries.ToListAsync();
            States = await _context.States.Include(s => s.Country).ToListAsync();
            Cities = await _context.Cities.Include(c => c.State).ToListAsync();
            Neighborhoods = await _context.Neighborhoods.Include(n => n.City).ToListAsync();
        }
    }
}
