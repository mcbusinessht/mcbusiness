using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Shipments_ShippingFees
{
    public class EditModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public EditModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ShippingFee ShippingFee { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shippingfee =  await _context.ShippingFees.FirstOrDefaultAsync(m => m.Id == id);
            if (shippingfee == null)
            {
                return NotFound();
            }
            ShippingFee = shippingfee;
            if (ShippingFee.BasePrice <= 0)
            {
                ShippingFee.BasePrice = ShippingFee.Price;
            }
           ViewData["CityId"] = new SelectList(_context.Cities, "Id", "Id");
           ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Address");
           ViewData["CountryId"] = new SelectList(_context.Countries, "Id", "Id");
           ViewData["CurrencyBase"] = new SelectList(_context.Currencies, "Id", "Id");
           ViewData["NeighborhoodId"] = new SelectList(_context.Neighborhoods, "Id", "Id");
           ViewData["StateId"] = new SelectList(_context.States, "Id", "Id");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            ShippingFee.Price = ShippingFee.BasePrice > 0 ? ShippingFee.BasePrice : ShippingFee.Price;
            ShippingFee.BasePrice = ShippingFee.BasePrice > 0 ? ShippingFee.BasePrice : ShippingFee.Price;

            _context.Attach(ShippingFee).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ShippingFeeExists(ShippingFee.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ShippingFeeExists(int id)
        {
            return _context.ShippingFees.Any(e => e.Id == id);
        }
    }
}
