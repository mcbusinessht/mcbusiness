using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
namespace MCBusiness.Services
{
    // ShipmentStatusHandler.cs
    public class ShipmentStatusHandler
    {
        private readonly IOrderStatusService _orderStatusService;
        private readonly AppDbContext _context;

        public ShipmentStatusHandler(
            IOrderStatusService orderStatusService,
            AppDbContext context)
        {
            _orderStatusService = orderStatusService;
            _context = context;
        }

        public async Task HandleShipmentUpdate(int shipmentId, ShipmentStatus newStatus)
        {
            var shipment = await _context.Shipments
                .Include(s => s.OrderProduct)
                .FirstOrDefaultAsync(s => s.Id == shipmentId);

            if (shipment == null) return;

            shipment.Status = newStatus;
            // UpdatedAt géré automatiquement par BaseEntity

            // Mapper le statut du shipment au statut du produit
            shipment.OrderProduct.Status = newStatus switch
            {
                ShipmentStatus.InTransit => OrderProductStatus.Shipped,
                ShipmentStatus.Delivered => OrderProductStatus.Delivered,
                ShipmentStatus.Canceled => OrderProductStatus.Cancelled,
                _ => shipment.OrderProduct.Status
            };

            await _context.SaveChangesAsync();
            await _orderStatusService.UpdateOrderStatusAsync(shipment.OrderProduct.OrderId);
        }
    }
}
