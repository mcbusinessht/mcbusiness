using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
#nullable enable



namespace MCBusiness.Pages.Users.Admin.GestionRoles
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [BindProperty]
        public RoleManagementModel Input { get; set; }

        public List<UserRoleViewModel> UsersWithRoles { get; set; } = new();
        public List<SelectListItem> AvailableRoles { get; set; } = new();
        public string UserId { get; set; }

        public class RoleManagementModel
        {
            [Required(ErrorMessage = "Sélectionnez un utilisateur")]
            [Display(Name = "Utilisateur")]
            public string UserId { get; set; }

            [Required(ErrorMessage = "Sélectionnez un rôle")]
            [Display(Name = "Rôle")]
            public string RoleName { get; set; }

            [Required(ErrorMessage = "Sélectionnez une action")]
            [Display(Name = "Action")]
            public string Action { get; set; } // "add" ou "remove"
        }

        public class UserRoleViewModel
        {
            public string UserId { get; set; }
            public string UserName { get; set; }
            public List<string> Roles { get; set; }
        }

        public async Task OnGetAsync(string? id)
        {
            if (id != null)
            {
                UserId = id;
            }
            await LoadData();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadData();
                return Page();
            }

            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Utilisateur introuvable");
                await LoadData();
                return Page();
            }

            IdentityResult result;
            if (Input.Action == "add")
            {
                result = await _userManager.AddToRoleAsync(user, Input.RoleName);
            }
            else
            {
                result = await _userManager.RemoveFromRoleAsync(user, Input.RoleName);
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            await LoadData();
            return Page();
        }

        private async Task LoadData()
        {
            // Solution 1: Utiliser ToList() avant la boucle
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            if (UserId != null)
            {
                users = await _userManager.Users
                .Where(u => u.Id == UserId)
                .OrderBy(u => u.UserName)
                .ToListAsync();
            }


            // Solution 2: Activer MARS (Multiple Active Result Sets) dans la cha�ne de connexion
            // Ajoutez "MultipleActiveResultSets=True" � votre connection string

            // Charger les rôles disponibles
            AvailableRoles = await _roleManager.Roles
                .Where(r => r.Name == "SecondAdmin" || r.Name == "Seller" || r.Name == "User" || r.Name == "Employee" || r.Name == "Ambassador")
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem(r.Name, r.Name))
                .ToListAsync();

            // Charger les utilisateurs avec leurs rôles
            UsersWithRoles = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                UsersWithRoles.Add(new UserRoleViewModel
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    Roles = userRoles.Where(r => r == "SecondAdmin" || r == "Seller" || r == "User" || r == "Employee" || r == "Ambassador").ToList()
                });
            }
        }
    }
}
