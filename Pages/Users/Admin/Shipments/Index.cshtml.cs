using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Shipments
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Shipment> Shipment { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Shipment = await _context.Shipments
                .Include(s => s.DeliveryPerson)
                .Include(s => s.OrderProduct).ToListAsync();
        }
    }
}
