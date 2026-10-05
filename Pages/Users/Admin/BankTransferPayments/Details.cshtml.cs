using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_BankTransferPayments
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public BankTransferPayment BankTransferPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            BankTransferPayment = await _context.BankTransferPayments
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (BankTransferPayment == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
