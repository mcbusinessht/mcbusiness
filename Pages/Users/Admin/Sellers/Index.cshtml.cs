using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Sellers
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<ApplicationUser> ApplicationUser { get; set; } = new List<ApplicationUser>();

        [BindProperty]
        public string ValueS { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var supplierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Seller");
            if (supplierRole != null)
            {
                var supplierIds = await _context.UserRoles
                    .Where(r => r.RoleId == supplierRole.Id)
                    .Select(r => r.UserId)
                    .ToListAsync();
                    
                ApplicationUser = await _context.Users
                    .Where(u => supplierIds.Contains(u.Id))
                    .OrderByDescending(u => u.RegistrationDate)
                    .ToListAsync();
            }
        }

        public async Task OnPostAsync()
        {
            var supplierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Seller");
            var query = _context.Users.AsQueryable();

            if (supplierRole != null)
            {
                var supplierIds = _context.UserRoles
                    .Where(r => r.RoleId == supplierRole.Id)
                    .Select(r => r.UserId);
                    
                query = query.Where(u => supplierIds.Contains(u.Id));
            }

            if (!string.IsNullOrEmpty(ValueS))
            {
                query = query.Where(u =>
                    (u.FirstName != null && u.FirstName.Contains(ValueS)) ||
                    (u.LastName != null && u.LastName.Contains(ValueS)) ||
                    (u.Email != null && u.Email.Contains(ValueS)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(ValueS)) ||
                    (u.UserName != null && u.UserName.Contains(ValueS)));
            }

            ApplicationUser = await query
                .OrderByDescending(u => u.RegistrationDate)
                .ToListAsync();
        }
    }
}
