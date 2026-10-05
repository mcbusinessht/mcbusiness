using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Services
{
    public class CartWithItems
    {
        public Cart Cart { get; set; }
        public List<CartItem> Items { get; set; }
        public decimal Subtotal { get; set; }
    }

    public interface IOptimizedCartService
    {
        Task<CartWithItems> GetCartWithItemsAsync(string userId);
        Task<bool> AddToCartAsync(string userId, int productVariantId, int quantity);
        Task<bool> UpdateCartItemQuantityAsync(string userId, int itemId, int newQuantity);
        Task<bool> RemoveFromCartAsync(string userId, int itemId);
    }

    public class OptimizedCartService : IOptimizedCartService
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;

        public OptimizedCartService(AppDbContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<CartWithItems> GetCartWithItemsAsync(string userId)
        {
            var cacheKey = $"cart_{userId}";
            
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
                if (cart == null)
                {
                    cart = new Cart { UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                var items = await _context.CartItems
                    .Where(ci => ci.CartId == cart.Id)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Currency)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Images.Take(1))
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.PriceBreaks)
                    .AsSplitQuery()
                    .ToListAsync();

                // Calculer le sous-total en utilisant les paliers de prix ou PriceSnapshot
                var subtotal = items.Sum(item => 
                    item.PriceSnapshot.HasValue ? 
                        item.Quantity * item.PriceSnapshot.Value : 
                        CalculateItemPrice(item.ProductVariant, item.Quantity));

                return new CartWithItems
                {
                    Cart = cart,
                    Items = items,
                    Subtotal = subtotal
                };
            }, TimeSpan.FromMinutes(5)); // Cache courte durée pour le panier
        }

        public async Task<bool> AddToCartAsync(string userId, int productVariantId, int quantity)
        {
            try
            {
                var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
                if (cart == null)
                {
                    cart = new Cart { UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                // Récupérer le variant avec les PriceBreaks pour calculer le bon prix
                var variant = await _context.ProductVariants
                    .Include(pv => pv.PriceBreaks)
                    .FirstOrDefaultAsync(pv => pv.Id == productVariantId);

                if (variant == null) return false;

                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductVariantId == productVariantId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    existingItem.UpdatedAt = DateTimeOffset.UtcNow;
                    
                    // Recalculer le prix basé sur la nouvelle quantité
                    var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, existingItem.Quantity);
                    existingItem.PriceSnapshot = applicablePriceBreak?.UnitPrice ?? variant.Price;
                }
                else
                {
                    // Calculer le prix basé sur les paliers pour la quantité demandée
                    var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, quantity);
                    var unitPrice = applicablePriceBreak?.UnitPrice ?? variant.Price;

                    var cartItem = new CartItem
                    {
                        CartId = cart.Id,
                        ProductVariantId = productVariantId,
                        Quantity = quantity,
                        PriceSnapshot = unitPrice,
                        AddedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();
                
                // Invalider le cache du panier
                _cache.Remove($"cart_{userId}");
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCartItemQuantityAsync(string userId, int itemId, int newQuantity)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.PriceBreaks)
                    .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == userId);

                if (cartItem == null) return false;

                if (newQuantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    // Vérifier le stock disponible
                    if (newQuantity > cartItem.ProductVariant.Quantity)
                        newQuantity = cartItem.ProductVariant.Quantity;

                    cartItem.Quantity = newQuantity;
                    cartItem.UpdatedAt = DateTimeOffset.UtcNow;
                    
                    // Recalculer le prix basé sur la nouvelle quantité
                    var applicablePriceBreak = FindApplicablePriceBreak(cartItem.ProductVariant.PriceBreaks, newQuantity);
                    cartItem.PriceSnapshot = applicablePriceBreak?.UnitPrice ?? cartItem.ProductVariant.Price;
                }

                await _context.SaveChangesAsync();
                
                // Invalider le cache du panier
                _cache.Remove($"cart_{userId}");
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(string userId, int itemId)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == userId);

                if (cartItem == null) return false;

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                
                // Invalider le cache du panier
                _cache.Remove($"cart_{userId}");
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Calcule le prix total d'un item en fonction des paliers de prix
        /// </summary>
        private decimal CalculateItemPrice(ProductVariant variant, int quantity)
        {
            if (variant.PriceBreaks?.Any() != true || quantity <= 0)
            {
                // Pas de paliers de prix ou quantité invalide, utiliser le prix de base
                return quantity * variant.Price;
            }

            var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, quantity);
            
            if (applicablePriceBreak != null)
            {
                return quantity * applicablePriceBreak.UnitPrice;
            }

            // Aucun palier applicable, utiliser le prix de base
            return quantity * variant.Price;
        }

        /// <summary>
        /// Trouve le palier de prix applicable pour une quantité donnée
        /// </summary>
        private PriceBreak FindApplicablePriceBreak(IEnumerable<PriceBreak> priceBreaks, int quantity)
        {
            if (priceBreaks == null || quantity <= 0) return null;

            // Filtrer les paliers actifs et non supprimés, puis trier par quantité minimum
            var activeBreaks = priceBreaks
                .Where(pb => pb.IsActive && !pb.IsDeleted)
                .OrderBy(pb => pb.MinQuantity)
                .ToList();

            // Trouver le bon palier en partant du plus élevé
            for (int i = activeBreaks.Count - 1; i >= 0; i--)
            {
                var priceBreak = activeBreaks[i];
                
                if (quantity >= priceBreak.MinQuantity)
                {
                    // Vérifier la quantité maximum si elle existe
                    if (priceBreak.MaxQuantity == null || quantity <= priceBreak.MaxQuantity)
                    {
                        return priceBreak;
                    }
                }
            }

            return null;
        }
    }
}
