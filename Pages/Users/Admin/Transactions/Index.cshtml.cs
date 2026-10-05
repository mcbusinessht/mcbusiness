using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Transactions
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Transaction> Transaction { get; set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.Transactions != null)
            {
                Transaction = await _context.Transactions.OrderByDescending(t => t.TransactionDate).ToListAsync();
            }
        }
    }
}
