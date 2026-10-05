using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public class OrderAmountService
    {

        private readonly AppDbContext _context;

        public OrderAmountService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Met à jour les OrderProducts en statut Pending si le prix du ProductVariant a changé.
        /// Recalcule aussi le montant total de la commande.
        /// </summary>
        public async Task UpdatePendingOrdersForProductVariantAsync(int productVariantId)
        {
            var variant = await _context.ProductVariants
                .Where(pv => pv.Id == productVariantId)
                .FirstOrDefaultAsync();

            if (variant == null) return;

            var affectedOrderProducts = await _context.OrderProducts
                .Include(op => op.Order)
                .Where(op => op.ProductVariantId == productVariantId && op.Order.Status == OrderStatus.Pending)
                .ToListAsync();

            foreach (var orderProduct in affectedOrderProducts)
            {
                orderProduct.PriceAtOrder = variant.Price;
            }

            var affectedOrders = affectedOrderProducts
                .Select(op => op.Order)
                .Distinct();

            foreach (var order in affectedOrders)
            {
                // CALCUL CORRIGÉ: Inclure les frais de livraison dans le total
                var productTotal = order.Products.Sum(p => p.Quantity * p.PriceAtOrder);
                var deliveryFeeTotal = order.Products.Sum(p => p.DeliveryFee);
                order.TotalAmount = productTotal + deliveryFeeTotal;
                // UpdatedAt géré automatiquement par BaseEntity (ModifiedAt) // TIMEZONE CORRIGÉE: Utiliser UTC
            }

            await _context.SaveChangesAsync();
        }

    }
}
