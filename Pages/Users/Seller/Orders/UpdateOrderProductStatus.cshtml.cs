using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.EntityFrameworkCore;


namespace MCBusiness.Pages.Users.Seller.Orders
{
    public class UpdateOrderProductStatusModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IOrderStatusService _orderStatusService;

        public UpdateOrderProductStatusModel(
            AppDbContext context,
            IOrderStatusService orderStatusService)
        {
            _context = context;
            _orderStatusService = orderStatusService;
        }

        [BindProperty]
        public OrderProduct OrderProduct { get; set; }

        [BindProperty]
        public OrderProductStatus NewStatus { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            OrderProduct = await _context.OrderProducts
                .Include(op => op.Order)
                .FirstOrDefaultAsync(op => op.Id == id);

            if (OrderProduct == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var orderProductToUpdate = await _context.OrderProducts
                .Include(op => op.Shipment)
                .Include(op => op.Order)
                .FirstOrDefaultAsync(op => op.Id == OrderProduct.Id);

            if (orderProductToUpdate == null)
            {
                return NotFound();
            }

            // Mise à jour du statut
            orderProductToUpdate.Status = NewStatus;
            
            // Mise à jour du shipment associé
            if (orderProductToUpdate.Shipment != null)
            {
                orderProductToUpdate.Shipment.Status = MapToShipmentStatus(NewStatus);
                // UpdatedAt géré automatiquement par BaseEntity

                if (NewStatus == OrderProductStatus.Delivered)
                {
                    orderProductToUpdate.Shipment.DeliveryDate = DateTimeOffset.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Mise à jour du statut de la commande parente
            await _orderStatusService.UpdateOrderStatusAsync(orderProductToUpdate.OrderId);

            return RedirectToPage("./Details", new { id = orderProductToUpdate.OrderId });
        }

        private ShipmentStatus MapToShipmentStatus(OrderProductStatus status)
        {
            return status switch
            {
                OrderProductStatus.Shipped => ShipmentStatus.InTransit,
                OrderProductStatus.Delivered => ShipmentStatus.Delivered,
                OrderProductStatus.Cancelled => ShipmentStatus.Canceled,
                _ => ShipmentStatus.Pending
            };
        }
    }
}
