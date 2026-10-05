using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using System.Security.Principal;

namespace MCBusiness.Pages.Users.Admin.Products.CharacteristicValues
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet(int? id)
        {
            if (id.HasValue)
            {
                ViewData["CharacteristicId"] = new SelectList(_context.Characteristics.Where(c => c.Id == id), "Id", "Name");
                CharacteristicId = id.Value;
            }
            else
            {
                ViewData["CharacteristicId"] = new SelectList(_context.Characteristics, "Id", "Name");
            }
            return Page();
        }

        [BindProperty]
        public int CharacteristicId { get; set; }

        [BindProperty]
        public string CharacteristicValuesRaw { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(CharacteristicValuesRaw))
            {
                return Page();
            }

            var values = CharacteristicValuesRaw.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(v => v.Trim())
                                                .Where(v => !string.IsNullOrEmpty(v))
                                                .Distinct();

            foreach (var value in values)
            {
                _context.CharacteristicValues.Add(new CharacteristicValue
                {
                    Value = value,
                    CharacteristicId = CharacteristicId
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
