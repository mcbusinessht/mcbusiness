using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public interface IOrderStatusService
    {
        Task UpdateOrderStatusAsync(int orderId);
    }

    public class OrderStatusService : IOrderStatusService
    {
        private readonly AppDbContext _context;
        private readonly OrderAmbassadorIntegrationService _ambassadorIntegration;
        private readonly ISellerPaymentService _sellerPaymentService;
        private readonly ILogger<OrderStatusService> _logger;

        public OrderStatusService(
            AppDbContext context,
            OrderAmbassadorIntegrationService ambassadorIntegration,
            ISellerPaymentService sellerPaymentService,
            ILogger<OrderStatusService> logger)
        {
            _context = context;
            _ambassadorIntegration = ambassadorIntegration;
            _sellerPaymentService = sellerPaymentService;
            _logger = logger;
        }

        public async Task UpdateOrderStatusAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Products)
                .ThenInclude(op => op.Shipment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return;
            }

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refunded)
            {
                return;
            }

            var products = order.Products.Where(p => !p.IsDeleted).ToList();

            if (products.Count == 0 || products.All(p => p.Status == OrderProductStatus.Cancelled))
            {
                order.Status = OrderStatus.Cancelled;
                await _context.SaveChangesAsync();
                return;
            }

            var activeProducts = products.Where(p => p.Status != OrderProductStatus.Cancelled).ToList();
            var newStatus = DetermineOrderStatus(activeProducts);

            if (order.Status != newStatus)
            {
                var previousStatus = order.Status;
                order.Status = newStatus;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Order {OrderId} status updated from {PreviousStatus} to {NewStatus}", orderId, previousStatus, newStatus);
            }

            await CreateEligibleSellerPaymentsAsync(orderId);
            await ProcessAmbassadorCommissionsIfReadyAsync(orderId, newStatus);
        }

        private async Task CreateEligibleSellerPaymentsAsync(int orderId)
        {
            var companyIds = await _context.OrderProducts
                .AsNoTracking()
                .Where(op => op.OrderId == orderId
                    && !op.IsDeleted
                    && op.Status != OrderProductStatus.Cancelled
                    && op.Status != OrderProductStatus.Returned)
                .Select(op => op.CompanyId)
                .Distinct()
                .ToListAsync();

            foreach (var companyId in companyIds)
            {
                try
                {
                    await _sellerPaymentService.CreateSellerPaymentIfEligibleAsync(orderId, companyId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create eligible seller payment for Order {OrderId}, Company {CompanyId}", orderId, companyId);
                }
            }
        }

        private async Task ProcessAmbassadorCommissionsIfReadyAsync(int orderId, OrderStatus newStatus)
        {
            if (newStatus != OrderStatus.Delivered)
            {
                return;
            }

            var hasCommission = await _context.AmbassadorCommissions
                .AsNoTracking()
                .AnyAsync(c => c.OrderId == orderId && !c.IsDeleted);

            if (hasCommission)
            {
                return;
            }

            try
            {
                var commissionsProcessed = await _ambassadorIntegration.ProcessCommissionsForDeliveredOrderAsync(orderId);
                if (commissionsProcessed)
                {
                    _logger.LogInformation("Ambassador commissions processed for delivered order {OrderId}", orderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ambassador commissions for order {OrderId}", orderId);
            }
        }

        private OrderStatus DetermineOrderStatus(List<OrderProduct> products)
        {
            if (products.Count == 0)
            {
                return OrderStatus.Cancelled;
            }

            var totalProducts = products.Count;
            var deliveredCount = products.Count(p => p.Status == OrderProductStatus.Delivered);
            var shippedCount = products.Count(p => p.Status == OrderProductStatus.Shipped);
            var returnedCount = products.Count(p => p.Status == OrderProductStatus.Returned);

            if (deliveredCount == totalProducts)
            {
                return OrderStatus.Delivered;
            }

            if (returnedCount == totalProducts)
            {
                return OrderStatus.Refunded;
            }

            if (deliveredCount > 0 && deliveredCount < totalProducts)
            {
                return OrderStatus.PartiallyDelivered;
            }

            if (shippedCount == totalProducts && deliveredCount == 0)
            {
                return OrderStatus.Shipped;
            }

            if (shippedCount > 0 && shippedCount < totalProducts && deliveredCount == 0)
            {
                return OrderStatus.PartiallyShipped;
            }

            if (returnedCount > 0 && returnedCount < totalProducts)
            {
                return OrderStatus.PartiallyReturned;
            }

            return OrderStatus.Paid;
        }
    }
}
