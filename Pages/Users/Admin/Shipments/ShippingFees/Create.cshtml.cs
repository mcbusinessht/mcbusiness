using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;

namespace MCBusiness.Pages_Users_Admin_Shipments_ShippingFees
{
    public class CreateModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;

        public CreateModel(MCBusiness.Models.AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["CityId"] = new SelectList(_context.Cities, "Id", "Id");
        ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Address");
        ViewData["CountryId"] = new SelectList(_context.Countries, "Id", "Id");
        ViewData["CurrencyBase"] = new SelectList(_context.Currencies, "Id", "Id");
        ViewData["NeighborhoodId"] = new SelectList(_context.Neighborhoods, "Id", "Id");
        ViewData["StateId"] = new SelectList(_context.States, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public ShippingFee ShippingFee { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            ShippingFee.Price = ShippingFee.BasePrice > 0 ? ShippingFee.BasePrice : ShippingFee.Price;
            ShippingFee.BasePrice = ShippingFee.BasePrice > 0 ? ShippingFee.BasePrice : ShippingFee.Price;

            _context.ShippingFees.Add(ShippingFee);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
