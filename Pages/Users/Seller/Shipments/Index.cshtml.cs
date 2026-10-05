using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.Seller.Shipments
{
    [Authorize(Roles = "Seller,Admin")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IOrderStatusService _orderStatusService;
        private readonly ISellerPaymentService _sellerPaymentService;
        private readonly OrderAmbassadorIntegrationService _ambassadorIntegrationService;

        public IndexModel(
            AppDbContext context,
            IOrderStatusService orderStatusService,
            ISellerPaymentService sellerPaymentService,
            OrderAmbassadorIntegrationService ambassadorIntegrationService)
        {
            _context = context;
            _orderStatusService = orderStatusService;
            _sellerPaymentService = sellerPaymentService;
            _ambassadorIntegrationService = ambassadorIntegrationService;
        }

        public IList<Shipment> Shipments { get; set; } = new List<Shipment>();

        [BindProperty(SupportsGet = true)]
        public string SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public ShipmentStatus? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            IQueryable<Shipment> query = _context.Shipments
                .Include(s => s.DeliveryPerson)
                .Include(s => s.OrderProduct)
                    .ThenInclude(op => op.Order)
                .Include(s => s.OrderProduct)
                    .ThenInclude(op => op.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .OrderByDescending(s => s.ShippingDate);

            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                query = query.Where(s =>
                    s.TrackingNumber.Contains(SearchString) ||
                    s.OrderProduct.ProductVariant.Product.Name.Contains(SearchString) ||
                    s.DeliveryPerson != null && s.DeliveryPerson.Name.Contains(SearchString));
            }

            if (StatusFilter.HasValue)
            {
                query = query.Where(s => s.Status == StatusFilter.Value);
            }

            Shipments = await query.Where(s => !s.IsDeleted).ToListAsync();
        }

        public async Task<IActionResult> OnPostMarkDeliveredAsync(int id)
        {
            return await ChangeShipmentStatusAsync(
                id,
                ShipmentStatus.Delivered,
                OrderProductStatus.Delivered,
                "Livraison marquee comme livree. Les enregistrements financiers eligibles ont ete relances.");
        }

        public async Task<IActionResult> OnPostCancelShipmentAsync(int id)
        {
            return await ChangeShipmentStatusAsync(
                id,
                ShipmentStatus.Canceled,
                OrderProductStatus.Returned,
                "Livraison annulee.");
        }

        public async Task<IActionResult> OnPostReconcileFinancialsAsync(int id)
        {
            var shipment = await _context.Shipments
                .Include(s => s.OrderProduct)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (shipment == null)
            {
                return NotFound();
            }

            if (shipment.Status != ShipmentStatus.Delivered || shipment.OrderProduct == null)
            {
                TempData["ErrorMessage"] = "Les ecritures financieres ne peuvent etre relancees que pour une livraison livree.";
                return RedirectToPage();
            }

            try
            {
                await _sellerPaymentService.CreateSellerPaymentIfEligibleAsync(
                    shipment.OrderProduct.OrderId,
                    shipment.OrderProduct.CompanyId);

                await _ambassadorIntegrationService.ProcessCommissionsForDeliveredOrderAsync(
                    shipment.OrderProduct.OrderId);

                TempData["SuccessMessage"] = "Les ecritures financieres de cette livraison ont ete verifiees et relancees.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"La relance financiere a echoue: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }

            if (shipment.Status == ShipmentStatus.Delivered)
            {
                TempData["ErrorMessage"] = "Une livraison deja livree ne peut pas etre supprimee.";
                return RedirectToPage();
            }

            _context.Shipments.Remove(shipment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Livraison supprimee avec succes.";
            return RedirectToPage();
        }

        private async Task<IActionResult> ChangeShipmentStatusAsync(
            int id,
            ShipmentStatus targetShipmentStatus,
            OrderProductStatus targetOrderProductStatus,
            string successMessage)
        {
            var shipment = await _context.Shipments
                .Include(s => s.OrderProduct)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (shipment == null)
            {
                return NotFound();
            }

            if (shipment.Status == ShipmentStatus.Delivered || shipment.Status == ShipmentStatus.Canceled)
            {
                TempData["ErrorMessage"] = "Cette livraison est deja finalisee et ne peut plus etre modifiee.";
                return RedirectToPage();
            }

            if (shipment.Status != ShipmentStatus.InTransit)
            {
                TempData["ErrorMessage"] = "Seules les livraisons en transit peuvent etre finalisees depuis cette page.";
                return RedirectToPage();
            }

            shipment.Status = targetShipmentStatus;
            if (targetShipmentStatus == ShipmentStatus.Delivered && shipment.DeliveryDate == null)
            {
                shipment.DeliveryDate = DateTimeOffset.UtcNow;
            }

            if (shipment.OrderProduct != null)
            {
                shipment.OrderProduct.Status = targetOrderProductStatus;
            }

            await _context.SaveChangesAsync();

            if (shipment.OrderProduct != null)
            {
                try
                {
                    await _orderStatusService.UpdateOrderStatusAsync(shipment.OrderProduct.OrderId);

                    if (targetShipmentStatus == ShipmentStatus.Delivered)
                    {
                        await _sellerPaymentService.CreateSellerPaymentIfEligibleAsync(
                            shipment.OrderProduct.OrderId,
                            shipment.OrderProduct.CompanyId);
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Livraison finalisee, mais les ecritures financieres n'ont pas ete comptabilisees: {ex.Message}";
                    return RedirectToPage();
                }
            }

            TempData["SuccessMessage"] = successMessage;
            return RedirectToPage();
        }
    }
}
