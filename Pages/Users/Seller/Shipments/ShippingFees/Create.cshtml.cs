using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages_Users_Seller_Shipments_ShippingFees
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            ShippingFee = new ShippingFee();
            await PopulateSelectListsAsync(user.Id);
            return Page();
        }

        [BindProperty]
        public ShippingFee ShippingFee { get; set; } = default!;

        // Handlers AJAX pour les listes dépendantes
        public async Task<JsonResult> OnGetStates(int id)
        {
            var states = await _context.States
                .Where(s => s.CountryId == id)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
            return new JsonResult(states);
        }

        public async Task<JsonResult> OnGetCities(int id)
        {
            var cities = await _context.Cities
                .Where(c => c.StateId == id)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return new JsonResult(cities);
        }

        // [IgnoreAntiforgeryToken]
        public async Task<JsonResult> OnGetNeighborhoods(int id)
        {
            var neighborhoods = await _context.Neighborhoods
                .Where(n => n.CityId == id)
                .Select(n => new { n.Id, n.Name })
                .ToListAsync();
            return new JsonResult(neighborhoods);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            RemoveEmptyValueErrors();
            ValidateShippingFeeInput();

            if (!ModelState.IsValid)
            {
                await PopulateSelectListsAsync(user.Id);
                return Page();
            }

            var ownsCompany = await _context.Companies
                .AnyAsync(c => c.Id == ShippingFee.CompanyId && c.SellerId == user.Id && !c.IsDeleted);
            if (!ownsCompany)
            {
                ModelState.AddModelError("ShippingFee.CompanyId", "Compagnie introuvable ou non autorisee pour ce vendeur.");
                await PopulateSelectListsAsync(user.Id);
                return Page();
            }

            ShippingFee.Price = ShippingFee.BasePrice;

            _context.ShippingFees.Add(ShippingFee);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private void ValidateShippingFeeInput()
        {
            if (ShippingFee.CompanyId <= 0)
            {
                ModelState.AddModelError("ShippingFee.CompanyId", "Selectionnez une compagnie de livraison.");
            }

            if (ShippingFee.CountryId <= 0)
            {
                ModelState.AddModelError("ShippingFee.CountryId", "Selectionnez un pays.");
            }

            if (ShippingFee.StateId <= 0)
            {
                ModelState.AddModelError("ShippingFee.StateId", "Selectionnez une region ou un departement.");
            }

            if (ShippingFee.CityId <= 0)
            {
                ModelState.AddModelError("ShippingFee.CityId", "Selectionnez une ville.");
            }

            if (ShippingFee.CurrencyBase <= 0)
            {
                ModelState.AddModelError("ShippingFee.CurrencyBase", "Selectionnez une devise.");
            }

            if (ShippingFee.MaxPrice.HasValue && ShippingFee.MinPrice.HasValue && ShippingFee.MaxPrice < ShippingFee.MinPrice)
            {
                ModelState.AddModelError("ShippingFee.MaxPrice", "Le maximum ne peut pas etre inferieur au minimum.");
            }
        }

        private void RemoveEmptyValueErrors()
        {
            RemoveIfPostedEmpty("ShippingFee.Price");
            RemoveIfPostedEmpty("ShippingFee.CompanyId");
            RemoveIfPostedEmpty("ShippingFee.CountryId");
            RemoveIfPostedEmpty("ShippingFee.StateId");
            RemoveIfPostedEmpty("ShippingFee.CityId");
            RemoveIfPostedEmpty("ShippingFee.CurrencyBase");
        }

        private void RemoveIfPostedEmpty(string key)
        {
            if (!ModelState.TryGetValue(key, out var entry))
            {
                return;
            }

            var isEmpty = entry.RawValue switch
            {
                null => true,
                string value => string.IsNullOrWhiteSpace(value),
                string[] values => values.All(string.IsNullOrWhiteSpace),
                _ => false
            };

            if (isEmpty)
            {
                ModelState.Remove(key);
            }
        }

        private async Task PopulateSelectListsAsync(string userId, ShippingFee selected = null)
        {
            var companies = await _context.Companies
                .AsNoTracking()
                .Where(c => c.SellerId == userId && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewData["CompanyId"] = new SelectList(companies, "Id", "Name", selected?.CompanyId ?? ShippingFee?.CompanyId);
            ViewData["CountryId"] = new SelectList(
                await _context.Countries.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name",
                selected?.CountryId ?? ShippingFee?.CountryId);
            ViewData["CurrencyBase"] = new SelectList(
                await _context.Currencies
                    .AsNoTracking()
                    .Where(c => c.Code == "HTG" && !c.IsDeleted)
                    .OrderBy(c => c.Code)
                    .ToListAsync(),
                "Id",
                "Code",
                selected?.CurrencyBase ?? ShippingFee?.CurrencyBase);

            if (!companies.Any())
            {
                ModelState.AddModelError(string.Empty, "Aucune compagnie vendeur active n'est associee a votre compte.");
            }
        }
    }
}
