using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Transactions
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Transaction Transaction { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Transaction = await _context.Transactions
                .Include(t => t.User)
                .Include(t => t.Currency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Transaction == null)
            {
                return NotFound();
            }
            return Page();
        }
    }
}
