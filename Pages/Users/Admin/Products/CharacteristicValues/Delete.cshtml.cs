using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products.CharacteristicValues
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public CharacteristicValue CharacteristicValue { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var characteristicvalue = await _context.CharacteristicValues.FirstOrDefaultAsync(m => m.Id == id);

            if (characteristicvalue == null)
            {
                return NotFound();
            }
            else
            {
                CharacteristicValue = characteristicvalue;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var characteristicvalue = await _context.CharacteristicValues.FindAsync(id);
            if (characteristicvalue != null)
            {
                CharacteristicValue = characteristicvalue;
                _context.CharacteristicValues.Remove(CharacteristicValue);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
