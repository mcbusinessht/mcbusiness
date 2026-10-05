using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products.Characteristics
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Characteristic Characteristic { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Chargez la caract�ristique avec sa catégorie associée
            Characteristic = await _context.Characteristics
                .Include(c => c.Category) // Important: inclure la catégorie
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Characteristic == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
