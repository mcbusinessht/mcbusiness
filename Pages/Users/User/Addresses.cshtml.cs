using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;

namespace MCBusiness.Pages.Users.User
{
    [Authorize]
    public class AddressesModel : PageModel
    {
        private readonly AppDbContext _context;
        public AddressesModel(AppDbContext context) => _context = context;

        public List<Address> Addresses { get; set; } = new();
        [BindProperty] public Address CurrentAddress { get; set; } = new();
        
        public SelectList CountryList { get; set; }
        public SelectList StateList { get; set; }
        public SelectList CityList { get; set; }
        public SelectList NeighborhoodList { get; set; }

        public async Task OnGetAsync()
        {
            var userId = GetUserId();
            if (userId == null) return;

            Addresses = await GetUserAddresses(userId);
            await InitializeSelectListsAsync();          
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
            {
                await InitializeSelectListsAsync();
                return Page();
            }

            await SaveAddressAsync(userId);
            return RedirectToPage();
        }

        // Nouveaux handlers pour les appels AJAX
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

        public async Task<JsonResult> OnGetNeighborhoods(int id)
        {
            var neighborhoods = await _context.Neighborhoods
                .Where(n => n.CityId == id)
                .Select(n => new { n.Id, n.Name })
                .ToListAsync();
            return new JsonResult(neighborhoods);
        }

        private string GetUserId()
        {
            return _context.Users
                .Where(u => u.UserName == User.Identity.Name)
                .Select(u => u.Id)
                .FirstOrDefault();
        }

        private async Task<List<Address>> GetUserAddresses(string userId)
        {
            return await _context.Addresses
                .Where(a => a.UserId == userId)
                .Include(a => a.Country)
                .Include(a => a.State)
                .Include(a => a.City)
                .Include(a => a.Neighborhood)
                .ToListAsync();
        }

        // Dans InitializeSelectListsAsync
        private async Task InitializeSelectListsAsync()
        {
            CountryList = new SelectList(await _context.Countries.ToListAsync(), "Id", "Name");
            
            if (CurrentAddress.CountryId > 0)
            {
                var states = await _context.States
                    .Where(s => s.CountryId == CurrentAddress.CountryId)
                    .ToListAsync();
                StateList = new SelectList(states, "Id", "Name", CurrentAddress.StateId);
            }
            
            if (CurrentAddress.StateId > 0)
            {
                var cities = await _context.Cities
                    .Where(c => c.StateId == CurrentAddress.StateId)
                    .ToListAsync();
                CityList = new SelectList(cities, "Id", "Name", CurrentAddress.CityId);
            }
            
            if (CurrentAddress.CityId > 0)
            {
                var neighborhoods = await _context.Neighborhoods
                    .Where(n => n.CityId == CurrentAddress.CityId)
                    .ToListAsync();
                NeighborhoodList = new SelectList(neighborhoods, "Id", "Name", CurrentAddress.NeighborhoodId);
            }
        }

        private async Task SaveAddressAsync(string userId)
        {
            CurrentAddress.UserId = userId;
            
            if (CurrentAddress.Id == 0)
            {
                _context.Addresses.Add(CurrentAddress);
            }
            else
            {
                _context.Attach(CurrentAddress).State = EntityState.Modified;
            }
            
            await _context.SaveChangesAsync();
        }
    
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userId = _context.Users
                          .Where(u => u.UserName == User.Identity.Name)
                          .Select(u => u.Id)
                          .FirstOrDefault();
            if (userId == null) return RedirectToPage("/Account/Login");

            var addr = await _context.Addresses
                        .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (addr != null)
            {
                _context.Addresses.Remove(addr);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
