using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Services;
using MCBusiness.Interfaces;

namespace MCBusiness.Pages.Users.Seller.Shipments
{
    [Authorize(Roles = "Seller,Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IOrderStatusService _ordeStatusService;

        public CreateModel(AppDbContext context, IOrderStatusService ordeStatusService)
        {
            _context = context;
            _ordeStatusService = ordeStatusService;
        }

        [BindProperty]
        public Shipment Shipment { get; set; }

        public SelectList Orders { get; set; }
        public SelectList OrderProducts { get; set; } = new SelectList(Enumerable.Empty<SelectListItem>());
        public SelectList DeliveryPersons { get; set; }
        public string TrackingNumber { get; set; }
        public int? SelectedOrderId { get; set; }
        public int? SelectedOrderProductId { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id, int? orderid)
        {
            TrackingNumber = GenerateCode();
            SelectedOrderId = orderid;
            SelectedOrderProductId = id;
            Shipment = new Shipment
            {
                OrderProductId = id ?? 0,
                TrackingNumber = TrackingNumber,
                ShippingDate = DateTimeOffset.UtcNow,
                Status = ShipmentStatus.InTransit
            };

            if (id.HasValue)
            {
                var allowedOrderProductQuery = await GetAllowedOrderProductQueryAsync();
                var orderProduct = await allowedOrderProductQuery
                    .FirstOrDefaultAsync(op => op.Id == id.Value);

                if (orderProduct == null)
                {
                    return NotFound();
                }

                SelectedOrderId = orderProduct.OrderId;

                if (!CanCreateShipmentForOrderProduct(orderProduct.Status))
                {
                    ModelState.AddModelError("Shipment.OrderProductId", "Ce produit de commande a deja une livraison ou n'est plus en attente.");
                }

                var shipmentExists = await _context.Shipments
                    .AnyAsync(s => s.OrderProductId == orderProduct.Id && !s.IsDeleted);
                if (shipmentExists)
                {
                    ModelState.AddModelError("Shipment.OrderProductId", "Une livraison existe deja pour ce produit de commande.");
                }
            }

            await LoadSelectLists(SelectedOrderId, SelectedOrderProductId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Shipment.DeliveryDate.HasValue && Shipment.DeliveryDate.Value < Shipment.ShippingDate)
            {
                ModelState.AddModelError("Shipment.DeliveryDate", "La date de livraison prevue ne peut pas etre avant la date d'expedition.");
            }

            if (Shipment.DeliveryPersonId <= 0)
            {
                ModelState.AddModelError("Shipment.DeliveryPersonId", "Veuillez selectionner un livreur.");
            }
            else
            {
                var deliveryPersonExists = await _context.DeliveryPersons
                    .AnyAsync(d => d.Id == Shipment.DeliveryPersonId && !d.IsDeleted);
                if (!deliveryPersonExists)
                {
                    ModelState.AddModelError("Shipment.DeliveryPersonId", "Livreur introuvable ou supprime.");
                }
            }

            var allowedOrderProductQuery = await GetAllowedOrderProductQueryAsync();
            var orderProduct = await allowedOrderProductQuery
                .FirstOrDefaultAsync(op => op.Id == Shipment.OrderProductId);

            if (orderProduct == null)
            {
                ModelState.AddModelError("Shipment.OrderProductId", "Produit de commande introuvable ou non autorise pour ce vendeur.");
            }
            else
            {
                SelectedOrderId = orderProduct.OrderId;
                SelectedOrderProductId = orderProduct.Id;

                if (!IsOrderReadyForShipment(orderProduct.Order.Status))
                {
                    ModelState.AddModelError("", "Cette commande doit etre payee avant de pouvoir creer une livraison.");
                }

                if (!CanCreateShipmentForOrderProduct(orderProduct.Status))
                {
                    ModelState.AddModelError("Shipment.OrderProductId", "Ce produit de commande a deja ete traite pour la livraison.");
                }

                var shipmentExists = await _context.Shipments
                    .AnyAsync(s => s.OrderProductId == orderProduct.Id && !s.IsDeleted);
                if (shipmentExists)
                {
                    ModelState.AddModelError("Shipment.OrderProductId", "Une livraison existe deja pour ce produit de commande.");
                }
            }

            if (!ModelState.IsValid)
            {
                TrackingNumber = Shipment?.TrackingNumber ?? GenerateCode();
                await LoadSelectLists(SelectedOrderId, SelectedOrderProductId);
                return Page();
            }

            Shipment.Status = NormalizeInitialShipmentStatus(Shipment.Status);
            orderProduct.Status = GetOrderProductStatusFromShipment(Shipment.Status);
            _context.OrderProducts.Update(orderProduct);

            _context.Shipments.Add(Shipment);
            await _context.SaveChangesAsync();
            await _ordeStatusService.UpdateOrderStatusAsync(orderProduct.OrderId);

            TempData["SuccessMessage"] = "Livraison creee avec succes";
            return RedirectToPage("./Index");
        }

        private async Task LoadSelectLists(int? selectedOrderId = null, int? selectedOrderProductId = null)
        {
            var allowedOrderProductQuery = await GetAllowedOrderProductQueryAsync();

            var orders = await allowedOrderProductQuery
                .Where(op => op.Status == OrderProductStatus.Pending || op.Status == OrderProductStatus.Shipped)
                .Select(op => op.Order)
                .Distinct()
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            Orders = new SelectList(orders, "Id", "Id", selectedOrderId);

            var orderProducts = Enumerable.Empty<SelectListItem>();
            if (selectedOrderId.HasValue)
            {
                orderProducts = await allowedOrderProductQuery
                    .Where(op => op.OrderId == selectedOrderId.Value
                        && (op.Status == OrderProductStatus.Pending || op.Status == OrderProductStatus.Shipped))
                    .OrderBy(op => op.ProductVariant.Product.Name)
                    .Select(op => new SelectListItem
                    {
                        Value = op.Id.ToString(),
                        Text = op.ProductVariant.Product.Name + " - Qte: " + op.Quantity,
                        Selected = selectedOrderProductId.HasValue && op.Id == selectedOrderProductId.Value
                    })
                    .ToListAsync();
            }

            OrderProducts = new SelectList(orderProducts, "Value", "Text", selectedOrderProductId?.ToString());
            DeliveryPersons = new SelectList(
                await _context.DeliveryPersons
                    .Where(d => !d.IsDeleted)
                    .OrderBy(d => d.Name)
                    .ToListAsync(),
                "Id",
                "Name",
                Shipment?.DeliveryPersonId);
        }

        public string GenerateCode()
        {
            string code;
            do
            {
                code = Random.Shared.NextInt64(100000000000, 999999999999).ToString();
            }
            while (_context.Shipments.Any(p => p.TrackingNumber == code));
            return code;
        }

        public async Task<JsonResult> OnGetProductsByOrderAsync(int orderId)
        {
            var products = await (await GetAllowedOrderProductQueryAsync())
                .Where(op => op.OrderId == orderId
                    && (op.Status == OrderProductStatus.Pending || op.Status == OrderProductStatus.Shipped))
                .OrderBy(op => op.ProductVariant.Product.Name)
                .Select(op => new
                {
                    id = op.Id,
                    name = op.ProductVariant.Product.Name + " - Qte: " + op.Quantity
                })
                .ToListAsync();

            return new JsonResult(products);
        }

        private async Task<IQueryable<OrderProduct>> GetAllowedOrderProductQueryAsync()
        {
            var shippableStatuses = new[]
            {
                OrderStatus.Paid,
                OrderStatus.PartiallyShipped,
                OrderStatus.PartiallyDelivered
            };

            var query = _context.OrderProducts
                .Include(op => op.Order)
                .Include(op => op.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                .Where(op => !op.IsDeleted && !op.Order.IsDeleted);

            if (!User.IsInRole("Admin"))
            {
                var userId = await _context.Users
                    .Where(u => u.UserName == User.Identity.Name)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();

                var companyId = await _context.Companies
                    .Where(c => c.SellerId == userId && !c.IsDeleted)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();

                query = companyId.HasValue
                    ? query.Where(op => op.CompanyId == companyId.Value)
                    : query.Where(op => false);
            }

            return query.Where(op => shippableStatuses.Contains(op.Order.Status));
        }

        private static bool IsOrderReadyForShipment(OrderStatus status)
        {
            return status == OrderStatus.Paid
                || status == OrderStatus.PartiallyShipped
                || status == OrderStatus.PartiallyDelivered;
        }

        private static bool CanCreateShipmentForOrderProduct(OrderProductStatus status)
        {
            return status == OrderProductStatus.Pending
                || status == OrderProductStatus.Shipped;
        }

        private static ShipmentStatus NormalizeInitialShipmentStatus(ShipmentStatus status)
        {
            return status == ShipmentStatus.Pending ? ShipmentStatus.InTransit : status;
        }

        private static OrderProductStatus GetOrderProductStatusFromShipment(ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Delivered => OrderProductStatus.Delivered,
                ShipmentStatus.Canceled => OrderProductStatus.Returned,
                ShipmentStatus.InTransit => OrderProductStatus.Shipped,
                _ => OrderProductStatus.Pending
            };
        }
    }
}
