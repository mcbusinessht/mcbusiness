using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public interface IStockReservationService
    {
        Task<bool> ReserveStockAsync(int productVariantId, int quantity, string userId, TimeSpan? reservationDuration = null);
        Task<bool> ReleaseReservationAsync(int productVariantId, int quantity, string userId);
        Task<int> GetAvailableStockAsync(int productVariantId);
        Task<bool> ConfirmReservationAsync(int productVariantId, int quantity, string userId);
        Task CleanupExpiredReservationsAsync();
        /// <summary>
        /// Réduit le stock pour chaque ligne de la commande. Idempotent : ne fait rien si la commande est déjà Payée.
        /// À appeler au moment de la confirmation du paiement (avant de passer Order.Status à Paid).
        /// </summary>
        Task<bool> ReduceStockForOrderAsync(int orderId);
    }

    public class StockReservationService : IStockReservationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockReservationService> _logger;
        private readonly TimeSpan _defaultReservationDuration = TimeSpan.FromMinutes(15);

        public StockReservationService(AppDbContext context, ILogger<StockReservationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ReserveStockAsync(int productVariantId, int quantity, string userId, TimeSpan? reservationDuration = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Nettoyer les réservations expirées d'abord
                await CleanupExpiredReservationsAsync();

                // Vérifier le stock disponible avec lock pessimiste
                var variant = await _context.ProductVariants
                    .Where(pv => pv.Id == productVariantId)
                    .FirstOrDefaultAsync();

                if (variant == null) return false;

                // Calculer stock réservé actuel
                var currentReservations = await _context.StockReservations
                    .Where(sr => sr.ProductVariantId == productVariantId && sr.ExpiresAt > DateTimeOffset.UtcNow)
                    .SumAsync(sr => sr.Quantity);

                var availableStock = variant.Quantity - currentReservations;

                if (availableStock < quantity)
                {
                    _logger.LogWarning("Stock insuffisant. Demandé: {Quantity}, Disponible: {Available}", quantity, availableStock);
                    return false;
                }

                // Créer la réservation
                var reservation = new StockReservation
                {
                    ProductVariantId = productVariantId,
                    UserId = userId,
                    Quantity = quantity,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(reservationDuration ?? _defaultReservationDuration),
                    Status = ReservationStatus.Active
                };

                _context.StockReservations.Add(reservation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Stock réservé: {quantity} unités du produit {productVariantId} pour {userId}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la réservation de stock");
                return false;
            }
        }

        public async Task<bool> ReleaseReservationAsync(int productVariantId, int quantity, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _context.StockReservations
                    .Where(sr => sr.ProductVariantId == productVariantId && 
                               sr.UserId == userId && 
                               sr.Quantity == quantity &&
                               sr.Status == ReservationStatus.Active)
                    .FirstOrDefaultAsync();

                if (reservation != null)
                {
                    reservation.Status = ReservationStatus.Released;
                    reservation.ReleasedAt = DateTimeOffset.UtcNow;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la libération de réservation");
                return false;
            }
        }

        public async Task<int> GetAvailableStockAsync(int productVariantId)
        {
            await CleanupExpiredReservationsAsync();

            var variant = await _context.ProductVariants.FindAsync(productVariantId);
            if (variant == null) return 0;

            var reservedStock = await _context.StockReservations
                .Where(sr => sr.ProductVariantId == productVariantId && 
                           sr.ExpiresAt > DateTimeOffset.UtcNow &&
                           sr.Status == ReservationStatus.Active)
                .SumAsync(sr => sr.Quantity);

            return Math.Max(0, variant.Quantity - reservedStock);
        }

        public async Task<bool> ConfirmReservationAsync(int productVariantId, int quantity, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _context.StockReservations
                    .Where(sr => sr.ProductVariantId == productVariantId && 
                               sr.UserId == userId && 
                               sr.Quantity == quantity &&
                               sr.Status == ReservationStatus.Active)
                    .FirstOrDefaultAsync();

                if (reservation == null) return false;

                // Confirmer la réservation et réduire le stock physique
                var variant = await _context.ProductVariants.FindAsync(productVariantId);
                if (variant == null) return false;

                variant.Quantity -= quantity;
                reservation.Status = ReservationStatus.Confirmed;
                reservation.ConfirmedAt = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Réservation confirmée et stock réduit: {quantity} unités du produit {productVariantId}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la confirmation de réservation");
                return false;
            }
        }

        public async Task CleanupExpiredReservationsAsync()
        {
            try
            {
                var expiredReservations = await _context.StockReservations
                    .Where(sr => sr.ExpiresAt <= DateTimeOffset.UtcNow && sr.Status == ReservationStatus.Active)
                    .ToListAsync();

                foreach (var reservation in expiredReservations)
                {
                    reservation.Status = ReservationStatus.Expired;
                    reservation.ReleasedAt = DateTimeOffset.UtcNow;
                }

                if (expiredReservations.Any())
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Nettoyage de {Count} réservations expirées", expiredReservations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du nettoyage des réservations expirées");
            }
        }

        /// <inheritdoc />
        public async Task<bool> ReduceStockForOrderAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                _logger.LogWarning("ReduceStockForOrder: commande {OrderId} introuvable", orderId);
                return false;
            }
            if (order.Status == OrderStatus.Paid)
            {
                _logger.LogInformation("ReduceStockForOrder: commande {OrderId} déjà payée, pas de réduction (idempotent)", orderId);
                return true;
            }

            foreach (var op in order.Products)
            {
                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductVariantId == op.ProductVariantId && !s.IsDeleted);
                if (stock != null)
                {
                    if (stock.Quantity < op.Quantity)
                    {
                        _logger.LogWarning("Stock insuffisant pour variant {VariantId}: disponible {Available}, demandé {Requested}", op.ProductVariantId, stock.Quantity, op.Quantity);
                        throw new InvalidOperationException($"Stock insuffisant pour le produit (variant {op.ProductVariantId}). Disponible: {stock.Quantity}, demandé: {op.Quantity}.");
                    }
                    stock.Quantity -= op.Quantity;
                    _context.Stocks.Update(stock);
                }
                else
                {
                    var variant = await _context.ProductVariants.FindAsync(op.ProductVariantId);
                    if (variant == null) continue;
                    if (variant.Quantity < op.Quantity)
                    {
                        _logger.LogWarning("Stock insuffisant pour variant {VariantId}: disponible {Available}, demandé {Requested}", op.ProductVariantId, variant.Quantity, op.Quantity);
                        throw new InvalidOperationException($"Stock insuffisant pour le produit (variant {op.ProductVariantId}). Disponible: {variant.Quantity}, demandé: {op.Quantity}.");
                    }
                    variant.Quantity -= op.Quantity;
                    _context.ProductVariants.Update(variant);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Stock réduit pour la commande {OrderId}", orderId);
            return true;
        }
    }

    // Modèles définis dans Models/StockReservation.cs
}
