using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Currencies
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Currency> Currency { get; set; } = default!;
        public string BaseCurrencySymbol { get; set; } = "HTG"; // Devise de base par défaut

        public async Task OnGetAsync()
        {
            Currency = await _context.Currencies
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }
    }
}
