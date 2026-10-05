using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Seller.Shipments
{
    [Authorize(Roles = "Seller,Admin")]
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Shipment Shipment { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Shipment = await _context.Shipments
                .Include(s => s.DeliveryPerson)
                .Include(s => s.OrderProduct)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Shipment == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Shipment = await _context.Shipments.FindAsync(id);

            if (Shipment != null)
            {
                Shipment.IsDeleted = true; // Marquer comme supprimé
                _context.Shipments.Update(Shipment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Livraison supprimée avec succès";
            }

            return RedirectToPage("./Index");
        }
    }
}
