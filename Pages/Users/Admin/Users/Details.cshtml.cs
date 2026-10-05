using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Users
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public ApplicationUser ApplicationUser { get; set; } = default!;
        public List<Address> Address { get; set; } = new List<Address>();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var applicationuser = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
            if (applicationuser == null)
            {
                return NotFound();
            }
            else
            {
                Address = await _context.Addresses.Where(a => a.UserId == applicationuser.Id).ToListAsync();
                ApplicationUser = applicationuser;
            }
            return Page();
        }
    }
}
