using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace MCBusiness.Pages.Users.Admin.Products.Characteristics
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Characteristic> Characteristic { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Characteristic = await _context.Characteristics
                .Include(c => c.Category).ToListAsync();
        }
    }
}
