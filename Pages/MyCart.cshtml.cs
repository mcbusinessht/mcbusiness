using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;

namespace MCBusiness.Pages
{
    public class MyCart : PageModel
    {
        private readonly ILogger<MyCart> _logger;
        private readonly AppDbContext _context;
        private readonly IAntiforgery _antiforgery;

        public MyCart(ILogger<MyCart> logger, AppDbContext context, IAntiforgery antiforgery)
        {
            _logger = logger;
            _context = context;
            _antiforgery = antiforgery;
        }

        public Cart Cart { get; private set; }
        public ApplicationUser CurrentUser { get; set; }
        public List<CartItem> CI { get; set; } = new List<CartItem>();
        public decimal SubTotal { get; set; } = 0;



        public async Task<IActionResult> OnGetAsync()  
        {
            _logger.LogInformation("User authenticated? {0}", User?.Identity?.IsAuthenticated);
            if (!User.Identity.IsAuthenticated)
                return RedirectToPage("/Account/Login", new { returnUrl = "/MyCart" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string username = User.Identity.Name;

            ApplicationUser currentUser = null;
            if (!string.IsNullOrEmpty(userId))
                currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (currentUser == null && !string.IsNullOrEmpty(username))
                currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

            if (currentUser == null)
            {
                _logger.LogWarning("Authenticated principal but user not found. userId={0} name={1}", userId, username);
                return RedirectToPage("/Account/Login", new { returnUrl = "/MyCart" });
            }

            CurrentUser = currentUser;

            // UTILISER / CRÉER le cart si absent
            Cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == CurrentUser.Id);
            if (Cart == null)
            {
                Cart = new Cart { UserId = CurrentUser.Id, CreatedAt = DateTimeOffset.UtcNow };
                _context.Carts.Add(Cart);
                await _context.SaveChangesAsync();
            }

            await LoadCartData(); // charge CI & SubTotal
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveItemAsync(int itemId)
        {
            CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (CurrentUser == null) return Unauthorized();

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == CurrentUser.Id);
            
            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            var summary = await GetCartSummaryAsync(CurrentUser.Id);
            return new JsonResult(new
            {
                success = true,
                subtotal = summary.Subtotal,
                itemCount = summary.ItemCount,
                currency = summary.CurrencySymbol
            });
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(int itemId, int change)
        {
            CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (CurrentUser == null) return Unauthorized();

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.ProductVariant)
                    .ThenInclude(pv => pv.Currency)
                .Include(ci => ci.ProductVariant)
                    .ThenInclude(pv => pv.PriceBreaks)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == CurrentUser.Id);

            if (cartItem == null)
            {
                return new JsonResult(new { success = false, message = "Article introuvable" })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            var newQuantity = cartItem.Quantity + change;
            if (newQuantity < 1) newQuantity = 1;
            if (newQuantity > cartItem.ProductVariant.Quantity)
            {
                newQuantity = cartItem.ProductVariant.Quantity;
            }

            cartItem.Quantity = newQuantity;
            cartItem.PriceSnapshot = CalculateUnitPriceWithBreaks(cartItem.ProductVariant, newQuantity);
            cartItem.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            var unitPrice = cartItem.PriceSnapshot.Value;
            var totalPrice = unitPrice * cartItem.Quantity;
            var summaryAfterUpdate = await GetCartSummaryAsync(CurrentUser.Id);
            var savings = cartItem.ProductVariant.Price - unitPrice;
            var savingsPercent = cartItem.ProductVariant.Price > 0
                ? savings / cartItem.ProductVariant.Price * 100
                : 0;

            return new JsonResult(new
            {
                success = true,
                quantity = cartItem.Quantity,
                maxStock = cartItem.ProductVariant.Quantity,
                unitPrice,
                basePrice = cartItem.ProductVariant.Price,
                totalPrice,
                subtotal = summaryAfterUpdate.Subtotal,
                itemCount = summaryAfterUpdate.ItemCount,
                currency = cartItem.ProductVariant.Currency?.Symbol ?? summaryAfterUpdate.CurrencySymbol,
                isTierPrice = unitPrice != cartItem.ProductVariant.Price,
                savingsPercent
            });
        }

        private async Task<(decimal Subtotal, int ItemCount, string CurrencySymbol)> GetCartSummaryAsync(string userId)
        {
            var items = await _context.CartItems
                .Where(ci => ci.Cart.UserId == userId)
                .Include(ci => ci.ProductVariant)
                    .ThenInclude(pv => pv.PriceBreaks)
                .Include(ci => ci.ProductVariant)
                    .ThenInclude(pv => pv.Currency)
                .ToListAsync();

            var subtotal = items.Sum(item =>
                item.PriceSnapshot.HasValue
                    ? item.Quantity * item.PriceSnapshot.Value
                    : CalculateItemPriceWithBreaks(item.ProductVariant, item.Quantity));

            return (
                subtotal,
                items.Count,
                items.FirstOrDefault()?.ProductVariant.Currency?.Symbol ?? string.Empty
            );
        }
        private async Task LoadCartData()
        {
            Cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == CurrentUser.Id);
            if (Cart != null)
            {
                // Optimisation: Utilisation d'AsSplitQuery pour éviter le problème cartésien
                CI = await _context.CartItems
                    .Where(ci => ci.CartId == Cart.Id)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Currency)
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Images.Take(1)) // Une seule image pour le panier
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.PriceBreaks) // Inclure les paliers de prix
                    .Include(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.ProductVariantCharacteristics.Take(3)) // Limiter les caractéristiques
                        .ThenInclude(pvc => pvc.CharacteristicValue)
                        .ThenInclude(cv => cv.Characteristic)
                    .AsSplitQuery() // Important pour les performances
                    .ToListAsync();

                // Calculer le sous-total en utilisant les paliers de prix ou PriceSnapshot
                SubTotal = CI.Sum(item => 
                    item.PriceSnapshot.HasValue ? 
                        item.Quantity * item.PriceSnapshot.Value : 
                        CalculateItemPriceWithBreaks(item.ProductVariant, item.Quantity));
            }
        }

        /// <summary>
        /// Calcule le prix total d'un item en fonction des paliers de prix
        /// </summary>
        private decimal CalculateItemPriceWithBreaks(ProductVariant variant, int quantity)
        {
            return quantity * CalculateUnitPriceWithBreaks(variant, quantity);
        }

        private decimal CalculateUnitPriceWithBreaks(ProductVariant variant, int quantity)
        {
            if (variant.PriceBreaks?.Any() != true || quantity <= 0)
            {
                return variant.Price;
            }

            var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, quantity);
            return applicablePriceBreak?.UnitPrice ?? variant.Price;
        }
        /// <summary>
        /// Trouve le palier de prix applicable pour une quantité donnée
        /// </summary>
        private PriceBreak FindApplicablePriceBreak(IEnumerable<PriceBreak> priceBreaks, int quantity)
        {
            if (priceBreaks == null || quantity <= 0) return null;

            var activeBreaks = priceBreaks
                .Where(pb => pb.IsActive && !pb.IsDeleted)
                .OrderBy(pb => pb.MinQuantity)
                .ToList();

            for (int i = activeBreaks.Count - 1; i >= 0; i--)
            {
                var priceBreak = activeBreaks[i];
                
                if (quantity >= priceBreak.MinQuantity)
                {
                    if (priceBreak.MaxQuantity == null || quantity <= priceBreak.MaxQuantity)
                    {
                        return priceBreak;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calcule le prix unitaire correct basé sur PriceSnapshot ou PriceBreaks
        /// </summary>
        public decimal GetCorrectUnitPrice(CartItem item)
        {
            // Si PriceSnapshot existe, l'utiliser (prix fixé lors de l'ajout au panier)
            if (item.PriceSnapshot.HasValue)
            {
                return item.PriceSnapshot.Value;
            }

            // Sinon, calculer avec les PriceBreaks actuels
            var applicablePriceBreak = FindApplicablePriceBreak(item.ProductVariant.PriceBreaks, item.Quantity);
            return applicablePriceBreak?.UnitPrice ?? item.ProductVariant.Price;
        }

        /// <summary>
        /// Calcule le prix total correct pour un item
        /// </summary>
        public decimal GetCorrectTotalPrice(CartItem item)
        {
            return item.Quantity * GetCorrectUnitPrice(item);
        }
    }
}
