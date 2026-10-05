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
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<CharacteristicValue> CharacteristicValue { get; set; } = default!;

        public async Task OnGetAsync(int? id)
        {
            if(id.HasValue){
                
                CharacteristicValue = await _context.CharacteristicValues
                    .Include(c => c.Characteristic)
                    .ThenInclude(ch=>ch.Category)
                    .Where(c => c.Characteristic.Id == id)
                    .ToListAsync();
            }else{
                CharacteristicValue = await _context.CharacteristicValues
                    .Include(c => c.Characteristic)
                    .ThenInclude(ch=>ch.Category)
                    .ToListAsync();
            }
        }
    }
}
