using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;


namespace MCBusiness.Services
{
    public class AddressService
    {
        private readonly AppDbContext _context;

        public AddressService(AppDbContext context)
        {
            _context = context;
        }

        // Ajouter une adresse pour un utilisateur
        public async Task<Address> AddUserAddressAsync(string userId, Address address, bool setAsDefault = false)
        {
            address.UserId = userId;
            address.CompanyId = null;
            address.AddressType = AddressType.Personal;

            if (setAsDefault)
            {
                await ResetDefaultAddressesAsync(userId: userId);
            }

            await _context.Addresses.AddAsync(address);
            await _context.SaveChangesAsync();
            return address;
        }

        // Ajouter une adresse pour une compagnie
        public async Task<Address> AddCompanyAddressAsync(int companyId, Address address, bool setAsDefault = false)
        {
            address.CompanyId = companyId;
            address.UserId = null;
            address.AddressType = AddressType.Business;

            if (setAsDefault)
            {
                await ResetDefaultAddressesAsync(companyId: companyId);
            }

            await _context.Addresses.AddAsync(address);
            await _context.SaveChangesAsync();
            return address;
        }

        // Réinitialiser les adresses par défaut
        private async Task ResetDefaultAddressesAsync(string? userId = null, int? companyId = null)
        {
            var addresses = await _context.Addresses
                .Where(a => a.IsDefault &&
                        ((userId != null && a.UserId == userId) ||
                            (companyId != null && a.CompanyId == companyId)))
                .ToListAsync();

            foreach (var addr in addresses)
            {
                addr.IsDefault = false;
            }
        }

        // Récupérer les adresses d'une compagnie
        public async Task<List<Address>> GetCompanyAddressesAsync(int companyId)
        {
            return await _context.Addresses
                .Where(a => a.CompanyId == companyId && !a.IsDeleted)
                .Include(a => a.Country)
                .Include(a => a.State)
                .Include(a => a.City)
                .Include(a => a.Neighborhood)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        // Récupérer l'adresse principale d'une compagnie
        public async Task<Address?> GetCompanyPrimaryAddressAsync(int companyId)
        {
            return await _context.Addresses
                .Where(a => a.CompanyId == companyId && a.IsDefault && !a.IsDeleted)
                .Include(a => a.Country)
                .Include(a => a.State)
                .Include(a => a.City)
                .Include(a => a.Neighborhood)
                .FirstOrDefaultAsync();
        }
    }
}
