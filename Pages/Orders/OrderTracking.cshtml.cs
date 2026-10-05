using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Orders
{
    [Authorize]
    public class OrderTrackingModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<OrderTrackingModel> _logger;

        public OrderTrackingModel(
            AppDbContext context, 
            UserManager<ApplicationUser> userManager,
            ILogger<OrderTrackingModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public Order Order { get; set; }
        public string PhoneNumber { get; set;}

        [BindProperty]
        public ReturnRequestInput ReturnRequest { get; set; }

        public class ReturnRequestInput
        {
            [Required]
            public int ProductId { get; set; }
            
            [Required]
            public string Reason { get; set; }
            
            public string Details { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found");
                    return NotFound();
                }

                Order = await LoadOrderWithRelatedData(id, user.Id);
                PhoneNumber = _context.Companies
                    .Where(c => c.SellerId == user.Id)
                    .Select(c => c.PhoneNumber)
                    .FirstOrDefault();                
                if (Order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found for user {UserId}", id, user.Id);
                    return NotFound();
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order {OrderId}", id);
                TempData["ErrorMessage"] = "Une erreur est survenue lors du chargement de la commande.";
                return RedirectToPage("/Orders/MyOrders");
            }
        }

        public async Task<IActionResult> OnPostRequestReturnAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return await HandleInvalidModelState();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                var (success, result) = await ProcessReturnRequest(user);
                if (!success)
                {
                    return result;
                }

                TempData["SuccessMessage"] = "Votre demande de retour a été soumise avec succès.";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing return request");
                TempData["ErrorMessage"] = "Une erreur est survenue lors du traitement de votre demande.";
                return RedirectToPage(new { id = ReturnRequest.ProductId });
            }
        }

        public async Task<IActionResult> OnPostConfirmDeliveryAsync(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Challenge();
                }

                var order = await _context.Orders
                    .Include(o => o.Products)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

                if (order == null)
                {
                    return NotFound();
                }

                if (order.IsReceived)
                {
                    TempData["SuccessMessage"] = "Reception de la commande deja confirmee.";
                    return RedirectToPage(new { id = order.Id });
                }

                var activeProducts = order.Products
                    .Where(p => p.Status != OrderProductStatus.Cancelled)
                    .ToList();

                if (!CanConfirmReceipt(order, activeProducts))
                {
                    TempData["ErrorMessage"] = "La reception ne peut etre confirmee que lorsque toute la commande a ete livree.";
                    return RedirectToPage(new { id = order.Id });
                }

                order.IsReceived = true;
                foreach (var product in activeProducts)
                {
                    product.IsReceived = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Order {OrderId} receipt confirmed from tracking page by user {UserId}",
                    order.Id,
                    user.Id);

                TempData["SuccessMessage"] = "Reception de la commande confirmee avec succes.";
                return RedirectToPage(new { id = order.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming delivery for order {OrderId}", id);
                TempData["ErrorMessage"] = "Une erreur est survenue lors de la confirmation de reception.";
                return RedirectToPage(new { id });
            }
        }

        private async Task<Order> LoadOrderWithRelatedData(int orderId, string userId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Address)
                    .ThenInclude(c=>c.Country)
                .Include(o => o.Address)
                    .ThenInclude(c=>c.State)
                .Include(o => o.Address)
                    .ThenInclude(c=>c.City) 
                .Include(o => o.Products)
                    .ThenInclude(p => p.ProductVariant)  
                .Include(o => o.Payment)
                .Include(o => o.Products)
                    .ThenInclude(p => p.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.Products)
                    .ThenInclude(p => p.ProductVariant)
                        .ThenInclude(pv => pv.Images.Take(1)) // Only load first image
                .Include(o => o.Products)
                    .ThenInclude(p => p.Shipment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        }

        private async Task<IActionResult> HandleInvalidModelState()
        {
            var orderId = await _context.OrderProducts
                .Where(op => op.Id == ReturnRequest.ProductId)
                .Select(op => op.OrderId)
                .FirstOrDefaultAsync();

            if (orderId == 0)
            {
                return NotFound();
            }

            Order = await LoadOrderWithRelatedData(orderId, (await _userManager.GetUserAsync(User))?.Id);
            return Page();
        }

        private async Task<(bool success, IActionResult result)> ProcessReturnRequest(ApplicationUser user)
        {
            var orderProduct = await _context.OrderProducts
                .Include(op => op.Order)
                .Include(op => op.Shipment)
                .FirstOrDefaultAsync(op => 
                    op.Id == ReturnRequest.ProductId && 
                    op.Order.UserId == user.Id);

            if (orderProduct == null)
            {
                return (false, NotFound());
            }

            if (!IsValidForReturn(orderProduct))
            {
                ModelState.AddModelError(string.Empty, "Le délai pour demander un retour est expiré.");
                return (false, await HandleInvalidModelState());
            }

            await CreateReturnRequest(orderProduct);
            return (true, RedirectToPage(new { id = orderProduct.OrderId }));
        }

        private bool IsValidForReturn(OrderProduct orderProduct)
        {
            return orderProduct.Status == OrderProductStatus.Delivered && 
                   orderProduct.Shipment?.DeliveryDate != null &&
                   (DateTimeOffset.UtcNow - orderProduct.Shipment.DeliveryDate.Value).TotalDays <= 7;
        }

        public bool CanConfirmReceipt()
        {
            if (Order == null)
            {
                return false;
            }

            var activeProducts = Order.Products?
                .Where(p => p.Status != OrderProductStatus.Cancelled)
                .ToList() ?? new List<OrderProduct>();

            return CanConfirmReceipt(Order, activeProducts);
        }

        private static bool CanConfirmReceipt(Order order, IReadOnlyCollection<OrderProduct> activeProducts)
        {
            return order.Status == OrderStatus.Delivered
                && !order.IsReceived
                && activeProducts.Count > 0
                && activeProducts.All(p => p.Status == OrderProductStatus.Delivered);
        }

        private async Task CreateReturnRequest(OrderProduct orderProduct)
        {
            var returnRequest = new ReturnRequest
            {
                OrderProductId = orderProduct.Id,
                RequestDate = DateTimeOffset.UtcNow,
                Reason = ReturnRequest.Reason,
                Details = ReturnRequest.Details,
                Status = ReturnStatus.Pending
            };

            _context.ReturnRequests.Add(returnRequest);
            orderProduct.Status = OrderProductStatus.ReturnRequested;
            await _context.SaveChangesAsync();
        }

        public string GetStatusBadgeClass(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "warning",
                OrderStatus.Paid => "info",
                OrderStatus.Shipped => "primary",
                OrderStatus.Delivered => "success",
                OrderStatus.Cancelled => "secondary",
                OrderStatus.Refunded => "light text-dark",
                _ => "secondary"
            };
        }

        public string GetProductStatusBadgeClass(OrderProductStatus? status)
        {
            return status switch
            {
                OrderProductStatus.Pending => "warning",
                OrderProductStatus.Shipped => "primary",
                OrderProductStatus.Delivered => "success",
                OrderProductStatus.Cancelled => "secondary",
                OrderProductStatus.Returned => "light text-dark",
                _ => "secondary"
            };
        }
    }
}

