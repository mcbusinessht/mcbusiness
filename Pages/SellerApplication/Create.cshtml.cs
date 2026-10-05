using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Pages.SellerApplication
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Le nom complet est requis")]
            [MaxLength(100)]
            public string FullName { get; set; }

            [Required(ErrorMessage = "L'email est requis")]
            [EmailAddress(ErrorMessage = "Email invalide")]
            [MaxLength(100)]
            public string Email { get; set; }

            [Required(ErrorMessage = "Le téléphone est requis")]
            [Phone(ErrorMessage = "Numéro de téléphone invalide")]
            [MaxLength(20)]
            public string PhoneNumber { get; set; }

            [Required(ErrorMessage = "Le nom de l'entreprise est requis")]
            [MaxLength(200)]
            public string BusinessName { get; set; }

            [Required(ErrorMessage = "Le type d'entreprise est requis")]
            [MaxLength(100)]
            public string BusinessType { get; set; }

            [MaxLength(1000)]
            public string Description { get; set; }

            [Required(ErrorMessage = "L'adresse est requise")]
            [MaxLength(200)]
            public string Address { get; set; }

            [Required(ErrorMessage = "La ville est requise")]
            [MaxLength(100)]
            public string City { get; set; }

            [Required(ErrorMessage = "Le pays est requis")]
            [MaxLength(100)]
            public string Country { get; set; } = "Haïti";

            [MaxLength(500)]
            [Url(ErrorMessage = "URL invalide")]
            public string IdentityDocumentUrl { get; set; }
        }

        public void OnGet()
        {
            // Initialize form
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Veuillez corriger les erreurs dans le formulaire.";
                return Page();
            }

            try
            {
                // Check if email already exists
                var existingApplication = await _context.SellerApplications
                    .AnyAsync(sa => sa.Email.ToLower() == Input.Email.ToLower() && !sa.IsDeleted);

                if (existingApplication)
                {
                    ErrorMessage = "Une candidature avec cet email existe déjà.";
                    return Page();
                }

                // Create new application
                var application = new Models.SellerApplication
                {
                    FullName = Input.FullName,
                    Email = Input.Email,
                    PhoneNumber = Input.PhoneNumber,
                    BusinessName = Input.BusinessName,
                    BusinessType = Input.BusinessType,
                    Description = Input.Description,
                    Address = Input.Address,
                    City = Input.City,
                    Country = Input.Country,
                    IdentityDocumentUrl = Input.IdentityDocumentUrl,
                    Status = SellerApplicationStatus.Pending
                };

                _context.SellerApplications.Add(application);
                await _context.SaveChangesAsync();

                SuccessMessage = "Votre candidature a été soumise avec succès ! Nous vous contacterons sous 48-72 heures.";
                
                // Clear form by redirecting
                return RedirectToPage("/SellerApplication/SendSuccessfully");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Une erreur s'est produite lors de la soumission. Veuillez réessayer.";
                // Log the error here if you have a logging service
                return Page();
            }
        }
    }
}
