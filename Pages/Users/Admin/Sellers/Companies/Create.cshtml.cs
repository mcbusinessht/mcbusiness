using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Admin.Sellers.Companies
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public CreateModel(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var suppliers = _context.Users
                    .Join(_context.UserRoles, user => user.Id, userRole => userRole.UserId, (user, userRole) => new { user, userRole })
                    .Join(_context.Roles, ur => ur.userRole.RoleId, role => role.Id, (ur, role) => new { ur.user, role })
                    .Where(x => x.role.Name == "Seller")
                    .Select(x => x.user)
                    .ToList();

            ViewData["FournisseurId"] = new SelectList(suppliers, "Id", "UserName");
            
            // Charger uniquement les cat?gories de type CompanySector
            var companySectorCategories = _context.Categories
                .Where(c => c.Type == CategoryType.CompanySector && !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();
            
            ViewData["CompanySectorCategories"] = companySectorCategories;
            
            // Charger les donn?es g?ographiques
            await LoadGeographicData();

            return Page();
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
        
        public string EmailError { get; set; }
        public string NameError { get; set; }
        
        // Propri?t?s pour les listes g?ographiques
        public List<Country> Countries { get; set; } = new List<Country>();
        public List<State> States { get; set; } = new List<State>();
        public List<City> Cities { get; set; } = new List<City>();

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

            ViewData["FournisseurId"] = new SelectList(suppliers, "Id", "UserName");
            
            var companySectorCategories = _context.Categories
                .Where(c => c.Type == CategoryType.CompanySector && !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();
            
            ViewData["CompanySectorCategories"] = companySectorCategories;
            
            // Recharger les donn?es g?ographiques en cas d'erreur
            await LoadGeographicData();

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
                Console.WriteLine("State non valide -----------------------------");
                return Page();
            }
            
            var isEmailExist = _context.Companies.Any(c => c.Email.ToUpper() == Company.Email.ToUpper());
            if (isEmailExist)
            {
                EmailError = "Attention, cet email est d?j? associ? ? une autre entreprise";
                Console.WriteLine("Erreur email -------------------------------");
                ModelState.AddModelError("EmailErrorKey", EmailError);
                return Page();
            }
            
            var isNameExist = _context.Companies.Any(c => c.Name.ToUpper() == Company.Name.ToUpper());
            if (isNameExist)
            {
                NameError = "Attention, ce nom est d?j? associ? ? une autre entreprise";
                Console.WriteLine("Name Error -------------------------------------------");
                ModelState.AddModelError("NameErrorKey", NameError);
                return Page();
            }

            // Cr?er l'entreprise avec audit automatique
            var userId = _auditService.GetCurrentUserId(User);
            _auditService.SetAuditFields(Company, userId);
            
            _context.Companies.Add(Company);
            await _context.SaveChangesAsync();

            // Cr?er l'adresse normalis?e
            var address = new Address
            {
                CompanyId = Company.Id,
                AddressType = AddressType.Business,
                Line1 = AddressLine1,
                CountryId = CountryId,
                StateId = StateId,
                CityId = CityId,
                IsDefault = true
            };
            
            _auditService.SetAuditFields(address, userId);
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();

            // Cr?er les CompanySectors avec audit automatique
            foreach (var categoryId in SelectedCompanySectorIds)
            {
                var companySector = new CompanySector
                {
                    CompanyId = Company.Id,
                    CategoryId = categoryId,
                    IsPrimary = PrimarySectorId.HasValue && PrimarySectorId.Value == categoryId
                    // AddedDate supprim? - remplac? par CreatedAt de BaseEntity
                };
                
                // Appliquer l'audit automatique si CompanySector h?rite de BaseEntity
                // _auditService.SetAuditFields(companySector, userId);
                
                _context.CompanySectors.Add(companySector);
            }
            
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        // M?thode pour charger les donn?es g?ographiques
        private async Task LoadGeographicData()
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
                return; // Pas besoin de valider le reste si le pays n'est pas s?lectionn?
            }

            // V?rifier que le pays existe
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

            // V?rifier que l'?tat existe et appartient au pays s?lectionn?
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

            // V?rifier que la ville existe et appartient ? l'?tat s?lectionn?
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
    }
}
