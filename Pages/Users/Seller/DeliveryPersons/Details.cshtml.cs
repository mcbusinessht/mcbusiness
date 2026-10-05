using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Seller.DeliveryPersons
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public DeliveryPerson DeliveryPerson { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var deliveryperson = await _context.DeliveryPersons.FirstOrDefaultAsync(m => m.Id == id);
            if (deliveryperson == null)
            {
                return NotFound();
            }
            else
            {
                DeliveryPerson = deliveryperson;
            }
            return Page();
        }
    }
}
