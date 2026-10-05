using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Addresses_Neighborhoods
{
    public class IndexModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public IndexModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IList<Neighborhood> Neighborhood { get;set; } = default!;

        public async Task OnGetAsync(int? id)
        {
            if(id.HasValue && id.Value >0){
                Neighborhood = await _context.Neighborhoods
                    .Include(n => n.City)
                    .Where(n=>n.City.Id== id)
                    .ToListAsync();
            }else{
                Neighborhood = await _context.Neighborhoods
                    .Include(n => n.City)
                    .ToListAsync();
            }
            
        }
    }
}
