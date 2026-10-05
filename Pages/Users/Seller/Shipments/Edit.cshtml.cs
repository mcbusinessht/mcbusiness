using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.Extensions.Logging;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.Seller.Shipments
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EditModel> _logger;
        private readonly IOrderStatusService _ordeStatusService ;
        public EditModel(AppDbContext context, ILogger<EditModel> logger, IOrderStatusService ordeStatusService)
        {
            _context = context;
            _logger = logger;
            _ordeStatusService = ordeStatusService;

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
                .Include(s => s.OrderProduct)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Shipment == null)
            {
                return NotFound();
            }

            await PopulateSelectLists();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid. Errors: {Errors}", 
                    string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)));
                
                await PopulateSelectLists();
                return Page();
            }

            // Verify OrderProduct exists
            var orderProductExists = await _context.OrderProducts
                .AnyAsync(op => op.Id == Shipment.OrderProductId);
                
            if (!orderProductExists)
            {
                ModelState.AddModelError("Shipment.OrderProductId", "The selected Order Product does not exist.");
                await PopulateSelectLists();
                return Page();
            }

            try
            {
                var existingShipment = await _context.Shipments.FindAsync(Shipment.Id);
                if (existingShipment == null)
                {
                    return NotFound();
                }

                // Update only the fields that should be changed
                existingShipment.DeliveryPersonId = Shipment.DeliveryPersonId;
                existingShipment.ShippingDate = Shipment.ShippingDate;
                existingShipment.DeliveryDate = Shipment.DeliveryDate;
                // UpdatedAt géré automatiquement par BaseEntity

                _logger.LogInformation("Updating shipment with ID {ShipmentId}", Shipment.Id);                     
                await _context.SaveChangesAsync();

                var orderProduct = await _context.OrderProducts
                    .FirstOrDefaultAsync(op => op.Id == existingShipment.OrderProductId);

                if (orderProduct == null)
                {
                    return NotFound();
                }

                orderProduct.Status = existingShipment.Status switch
                {
                    ShipmentStatus.InTransit => OrderProductStatus.Shipped,
                    ShipmentStatus.Delivered => OrderProductStatus.Delivered,
                    ShipmentStatus.Canceled => OrderProductStatus.Returned,
                    _ => orderProduct.Status
                };

                await _context.SaveChangesAsync();

                await _ordeStatusService.UpdateOrderStatusAsync(orderProduct.OrderId);   
                _logger.LogInformation("Successfully updated shipment with ID {ShipmentId}", Shipment.Id);  

                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!ShipmentExists(Shipment.Id))
                {
                    return NotFound();
                }
                _logger.LogError(ex, "Concurrency error updating shipment with ID {ShipmentId}", Shipment.Id);
                ModelState.AddModelError("", "The record you attempted to edit was modified by another user. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shipment with ID {ShipmentId}", Shipment.Id);
                ModelState.AddModelError("", "An unexpected error occurred while saving. Please try again.");
            }

            await PopulateSelectLists();
            return Page();
        }

        private bool ShipmentExists(int id)
        {
            return _context.Shipments.Any(e => e.Id == id);
        }

        private async Task PopulateSelectLists()
        {
            ViewData["DeliveryPersonId"] = new SelectList(
                await _context.DeliveryPersons.OrderBy(d => d.Name).ToListAsync(),
                "Id", 
                "Name", 
                Shipment?.DeliveryPersonId);

            ViewData["OrderProductId"] = new SelectList(
                await _context.OrderProducts
                    .Include(op => op.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                    .OrderBy(op => op.Id)
                    .ToListAsync(),
                "Id", 
                "Name", 
                Shipment?.OrderProductId);
        }
    }
}
