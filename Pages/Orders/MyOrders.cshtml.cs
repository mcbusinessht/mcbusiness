using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Orders
{
    [Authorize]
    public class MyOrdersModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MyOrdersModel> _logger;
        private const int PageSize = 10;

        public MyOrdersModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<MyOrdersModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public List<Order> Orders { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }

        public async Task OnGetAsync(int page = 1)
        {
            try
            {
                CurrentPage = page;
                var user = await _userManager.GetUserAsync(User);
                
                if (user != null)
                {
                    var query = _context.Orders
                        .Where(o => o.UserId == user.Id)
                        .Include(o => o.Payment)
                        .Include(o => o.Currency)
                        .OrderByDescending(o => o.OrderDate);

                    var totalItems = await query.CountAsync();
                    TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

                    Orders = await query
                        .Skip((page - 1) * PageSize)
                        .Take(PageSize)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders");
                TempData["ErrorMessage"] = "Une erreur est survenue lors du chargement de vos commandes.";
            }
        }

        public async Task<IActionResult> OnPostCancelAsync(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

                if (order != null && (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Paid))
                {
                    order.Status = OrderStatus.Cancelled;
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "La commande a été annulée avec succès.";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", id);
                TempData["ErrorMessage"] = "Une erreur est survenue lors de l'annulation de la commande.";
                return RedirectToPage();
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
                    TempData["SuccessMessage"] = "Réception de la commande confirmée avec succès.";
                    return RedirectToPage();
                }

                var activeProducts = order.Products
                    .Where(p => p.Status != OrderProductStatus.Cancelled)
                    .ToList();

                if (order.Status != OrderStatus.Delivered ||
                    activeProducts.Count == 0 ||
                    activeProducts.Any(p => p.Status != OrderProductStatus.Delivered))
                {
                    TempData["ErrorMessage"] = "La reception ne peut etre confirmee que lorsque toute la commande a ete livree.";
                    return RedirectToPage();
                }

                order.IsReceived = true;
                foreach (var product in activeProducts)
                {
                    product.IsReceived = true;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Order {OrderId} receipt confirmed by user {UserId}",
                    order.Id,
                    user.Id);
                TempData["SuccessMessage"] = "Reception de la commande confirmee avec succes.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming delivery for order {OrderId}", id);
                TempData["ErrorMessage"] = "Une erreur est survenue lors de la confirmation de réception.";
                return RedirectToPage();
            }
        }

        public string GetStatusBadgeClass(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "bg-warning",
                OrderStatus.Paid => "bg-info",
                OrderStatus.Shipped => "bg-primary",
                OrderStatus.Delivered => "bg-success",
                OrderStatus.Cancelled => "bg-secondary",
                OrderStatus.Refunded => "bg-light text-dark",
                _ => "bg-secondary"
            };
        }
    }
}
