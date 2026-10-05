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
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.Admin.Sellers.Companies
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public EditModel(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [BindProperty]
        public Company Company { get; set; } = default!;
        
        [BindProperty]
        public List<int> SelectedCompanySectorIds { get; set; } = new List<int>();
        
        [BindProperty]
        public int? PrimarySectorId { get; set; }
        
        // Champs d'adresse normalis?s
        [BindProperty]
        public string AddressLine1 { get; set; }
        
        [BindProperty]
        public int CountryId { get; set; }
        
        [BindProperty]
        public int StateId { get; set; }
        
        [BindProperty]
        public int CityId { get; set; }
        
        // Propri?t?s pour les listes g?ographiques
        public List<Country> Countries { get; set; } = new List<Country>();
        public List<State> States { get; set; } = new List<State>();
        public List<City> Cities { get; set; } = new List<City>();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var company = await _context.Companies
                .Include(c => c.CompanySectors)
                .ThenInclude(cs => cs.Category)
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Country)
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.State)
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.City)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (company == null)
            {
                return NotFound();
            }
            
            Company = company;
            
            // Charger les secteurs actuels de l'entreprise
            SelectedCompanySectorIds = company.CompanySectors.Select(cs => cs.CategoryId).ToList();
            PrimarySectorId = company.CompanySectors.FirstOrDefault(cs => cs.IsPrimary)?.CategoryId;
            
            // Charger l'adresse principale existante
            var primaryAddress = company.Addresses.FirstOrDefault(a => a.IsDefault && !a.IsDeleted);
            if (primaryAddress != null)
            {
                AddressLine1 = primaryAddress.Line1;
                CountryId = primaryAddress.CountryId;
                StateId = primaryAddress.StateId;
                CityId = primaryAddress.CityId;
            }
            
            // Charger les fournisseurs
            var suppliers = _context.Users
                .Join(_context.UserRoles, user => user.Id, userRole => userRole.UserId, (user, userRole) => new { user, userRole })
                .Join(_context.Roles, ur => ur.userRole.RoleId, role => role.Id, (ur, role) => new { ur.user, role })
                .Where(x => x.role.Name == "Seller")
                .Select(x => x.user)
                .ToList();
            
            ViewData["FournisseurId"] = new SelectList(suppliers, "Id", "UserName", Company.SellerId);
            
            // Charger les cat?gories de type CompanySector
            var companySectorCategories = _context.Categories
                .Where(c => c.Type == CategoryType.CompanySector && !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();
            
            ViewData["CompanySectorCategories"] = companySectorCategories;
            
            // Charger les donn?es g?ographiques
            await LoadGeographicDataAsync();
            
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Recharger les donn?es pour la vue en cas d'erreur
            var suppliers = _context.Users
                .Join(_context.UserRoles, user => user.Id, userRole => userRole.UserId, (user, userRole) => new { user, userRole })
                .Join(_context.Roles, ur => ur.userRole.RoleId, role => role.Id, (ur, role) => new { ur.user, role })
                .Where(x => x.role.Name == "Seller")
                .Select(x => x.user)
                .ToList();

            ViewData["FournisseurId"] = new SelectList(suppliers, "Id", "UserName", Company.SellerId);
            
            var companySectorCategories = _context.Categories
                .Where(c => c.Type == CategoryType.CompanySector && !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();
            
            ViewData["CompanySectorCategories"] = companySectorCategories;
            
            // Recharger les donn?es g?ographiques en cas d'erreur
            await LoadGeographicDataAsync();

            // Validation des secteurs s?lectionn?s
            if (SelectedCompanySectorIds == null || !SelectedCompanySectorIds.Any())
            {
                ModelState.AddModelError("SelectedCompanySectorIds", "Veuillez s?lectionner au moins un secteur d'activit?.");
            }
            
            // Si un secteur primaire est s?lectionn?, il doit ?tre dans la liste des secteurs s?lectionn?s
            if (PrimarySectorId.HasValue && (SelectedCompanySectorIds == null || !SelectedCompanySectorIds.Contains(PrimarySectorId.Value)))
            {
                ModelState.AddModelError("PrimarySectorId", "Le secteur primaire doit ?tre s?lectionn? dans la liste des secteurs.");
            }
            
            // Validation g?ographique obligatoire
            await ValidateGeographicDataAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var userId = _auditService.GetCurrentUserId(User);
                
                // Mettre ? jour l'entreprise
                _context.Attach(Company).State = EntityState.Modified;

                // Mettre ? jour ou cr?er l'adresse principale
                var existingAddress = await _context.Addresses
                    .FirstOrDefaultAsync(a => a.CompanyId == Company.Id && a.IsDefault && !a.IsDeleted);

                if (existingAddress != null)
                {
                    // Mettre ? jour l'adresse existante
                    existingAddress.Line1 = AddressLine1;
                    existingAddress.CountryId = CountryId;
                    existingAddress.StateId = StateId;
                    existingAddress.CityId = CityId;
                    _auditService.SetAuditFields(existingAddress, userId);
                    _context.Update(existingAddress);
                }
                else
                {
                    // Cr?er une nouvelle adresse
                    var newAddress = new Address
                    {
                        CompanyId = Company.Id,
                        AddressType = AddressType.Business,
                        Line1 = AddressLine1,
                        CountryId = CountryId,
                        StateId = StateId,
                        CityId = CityId,
                        IsDefault = true
                    };
                    _auditService.SetAuditFields(newAddress, userId);
                    _context.Addresses.Add(newAddress);
                }

                // Supprimer les anciens secteurs de l'entreprise
                var existingCompanySectors = _context.CompanySectors.Where(cs => cs.CompanyId == Company.Id);
                _context.CompanySectors.RemoveRange(existingCompanySectors);

                // Ajouter les nouveaux secteurs
                foreach (var categoryId in SelectedCompanySectorIds)
                {
                    var companySector = new CompanySector
                    {
                        CompanyId = Company.Id,
                        CategoryId = categoryId,
                        IsPrimary = PrimarySectorId.HasValue && PrimarySectorId.Value == categoryId,
                        AddedDate = DateTimeOffset.UtcNow
                    };
                    
                    _context.CompanySectors.Add(companySector);
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CompanyExists(Company.Id))
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

        // M?thode pour charger les donn?es g?ographiques
        private async Task LoadGeographicDataAsync()
        {
            Countries = await _context.Countries
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // Validation stricte des donn?es g?ographiques
        private async Task ValidateGeographicDataAsync()
        {
            if (string.IsNullOrWhiteSpace(AddressLine1))
            {
                ModelState.AddModelError(nameof(AddressLine1), "L'adresse est obligatoire.");
            }

            if (CountryId <= 0)
            {
                ModelState.AddModelError(nameof(CountryId), "Le pays est obligatoire.");
                return;
            }

            var countryExists = await _context.Countries.AnyAsync(c => c.Id == CountryId && !c.IsDeleted);
            if (!countryExists)
            {
                ModelState.AddModelError(nameof(CountryId), "Le pays s?lectionn? n'existe pas.");
                return;
            }

            if (StateId <= 0)
            {
                ModelState.AddModelError(nameof(StateId), "L'?tat/d?partement est obligatoire.");
                return;
            }

            var stateExists = await _context.States.AnyAsync(s => s.Id == StateId && s.CountryId == CountryId && !s.IsDeleted);
            if (!stateExists)
            {
                ModelState.AddModelError(nameof(StateId), "L'?tat s?lectionn? n'existe pas ou n'appartient pas au pays choisi.");
                return;
            }

            if (CityId <= 0)
            {
                ModelState.AddModelError(nameof(CityId), "La ville est obligatoire.");
                return;
            }

            var cityExists = await _context.Cities.AnyAsync(c => c.Id == CityId && c.StateId == StateId && !c.IsDeleted);
            if (!cityExists)
            {
                ModelState.AddModelError(nameof(CityId), "La ville s?lectionn?e n'existe pas ou n'appartient pas ? l'?tat choisi.");
            }
        }

        // API pour r?cup?rer les ?tats d'un pays
        public async Task<IActionResult> OnGetStatesAsync(int countryId)
        {
            var states = await _context.States
                .Where(s => s.CountryId == countryId && !s.IsDeleted)
                .OrderBy(s => s.Name)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return new JsonResult(states);
        }

        // API pour r?cup?rer les villes d'un ?tat
        public async Task<IActionResult> OnGetCitiesAsync(int stateId)
        {
            var cities = await _context.Cities
                .Where(c => c.StateId == stateId && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.Id, name = c.Name })
                .ToListAsync();

            return new JsonResult(cities);
        }

        private bool CompanyExists(int id)
        {
            return _context.Companies.Any(e => e.Id == id);
        }
    }
}
