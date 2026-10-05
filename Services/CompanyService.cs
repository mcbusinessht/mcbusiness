using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;


namespace MCBusiness.Services
{
    public class CompanyService
    {
        private readonly AppDbContext _context;

        public CompanyService(AppDbContext context)
        {
            _context = context;
        }

        // Ajouter un secteur à une compagnie
        public async Task AddSectorToCompanyAsync(int companyId, int categoryId, bool isPrimary = false)
        {
            var existingSector = await _context.CompanySectors
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.CategoryId == categoryId);

            if (existingSector != null)
                return; // Le secteur existe déjà

            // Si on définit ce secteur comme primaire, retirer le statut primaire des autres
            if (isPrimary)
            {
                var currentPrimary = await _context.CompanySectors
                    .Where(cs => cs.CompanyId == companyId && cs.IsPrimary)
                    .ToListAsync();

                foreach (var sector in currentPrimary)
                {
                    sector.IsPrimary = false;
                }
            }

            var companySector = new CompanySector
            {
                CompanyId = companyId,
                CategoryId = categoryId,
                IsPrimary = isPrimary
            };

            await _context.CompanySectors.AddAsync(companySector);
            await _context.SaveChangesAsync();
        }

        // Récupérer les secteurs d'une compagnie
        public async Task<List<Category>> GetCompanySectorsAsync(int companyId)
        {
            return await _context.CompanySectors
                .Where(cs => cs.CompanyId == companyId)
                .Include(cs => cs.Category)
                .Select(cs => cs.Category)
                .ToListAsync();
        }

        // Récupérer le secteur principal d'une compagnie
        public async Task<Category?> GetPrimarySectorAsync(int companyId)
        {
            return await _context.CompanySectors
                .Where(cs => cs.CompanyId == companyId && cs.IsPrimary)
                .Include(cs => cs.Category)
                .Select(cs => cs.Category)
                .FirstOrDefaultAsync();
        }

        // Supprimer un secteur d'une compagnie
        public async Task RemoveSectorFromCompanyAsync(int companyId, int categoryId)
        {
            var companySector = await _context.CompanySectors
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.CategoryId == categoryId);

            if (companySector != null)
            {
                _context.CompanySectors.Remove(companySector);
                await _context.SaveChangesAsync();
            }
        }
    }
}
