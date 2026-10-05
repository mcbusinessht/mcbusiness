using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;
using System.Security.Claims;

namespace MCBusiness.Pages
{
    [MCBusiness.Attributes.IdempotentAction(timeoutSeconds: 15, keyPrefix: "PlaceOrder")]
    public class PlaceOrderModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly OrderAmbassadorIntegrationService _ambassadorIntegration;
        private readonly ILogger<PlaceOrderModel> _logger;

        public PlaceOrderModel(
            AppDbContext context, 
            IAuditService auditService,
            OrderAmbassadorIntegrationService ambassadorIntegration,
            ILogger<PlaceOrderModel> logger)
        {
            _context = context;
            _auditService = auditService;
            _ambassadorIntegration = ambassadorIntegration;
            _logger = logger;
        }

        public List<CartItem> CartItems { get; set; } = new();
        public List<Address> UserAddresses { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal ShippingTotal { get; set; }
        public decimal GrandTotal => SubTotal + ShippingTotal;
        public List<ShippingFeeDisplay> ShippingFees { get; set; } = new();
        public Dictionary<int, decimal> DeliveryFeeByVariantId { get; set; } = new();
        public int CartId { get; set; }

        [BindProperty]
        public int SelectedAddressId { get; set; }

        public bool ShippingAvailable { get; set; } = true;

        public class ShippingFeeDisplay
        {
            public int CompanyId { get; set; }
            public string CompanyName { get; set; }
            public decimal Amount { get; set; }
            public bool IsAvailable { get; set; }
            public bool QuoteRequired { get; set; }
            public string Message { get; set; }
            public List<ShippingFeeLineDisplay> Lines { get; set; } = new();
        }

        public class ShippingFeeLineDisplay
        {
            public int ProductVariantId { get; set; }
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public ShippingPackageType PackageType { get; set; }
            public decimal Amount { get; set; }
            public bool IsPrimary { get; set; }
            public bool IsBlocking { get; set; }
            public bool CanRemoveFromCart { get; set; }
            public string Explanation { get; set; }
            public string ActionHint { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user == null) return RedirectToPage("/Account/Login");

            await LoadCartData(user.Id);
            await LoadAddressData(user.Id);

            if (UserAddresses.Any() && SelectedAddressId == 0)
                SelectedAddressId = UserAddresses.First().Id;

            await CalculateShippingFees(CartItems, SelectedAddressId);

            return Page();
        }

        public async Task<JsonResult> OnGetShippingFeesAsync(int addressId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return new JsonResult(new { error = "Utilisateur non connecte" });
            }

            var ownsAddress = await _context.Addresses
                .AnyAsync(a => a.Id == addressId && a.UserId == userId);
            if (!ownsAddress)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return new JsonResult(new { error = "Adresse introuvable" });
            }

            await LoadCartData(userId);
            await CalculateShippingFees(CartItems, addressId);

            return new JsonResult(new {
                shippingFees = ShippingFees.Select(f => new {
                    companyId = f.CompanyId,
                    companyName = f.CompanyName,
                    amount = f.Amount,
                    isAvailable = f.IsAvailable,
                    quoteRequired = f.QuoteRequired,
                    message = f.Message,
                    lines = f.Lines.Select(l => new {
                        productVariantId = l.ProductVariantId,
                        productName = l.ProductName,
                        quantity = l.Quantity,
                        packageType = l.PackageType.ToString(),
                        amount = l.Amount,
                        isPrimary = l.IsPrimary,
                        isBlocking = l.IsBlocking,
                        canRemoveFromCart = l.CanRemoveFromCart,
                        explanation = l.Explanation,
                        actionHint = l.ActionHint
                    })
                }),
                shippingTotal = ShippingTotal,
                grandTotal = SubTotal + ShippingTotal,
                shippingAvailable = ShippingAvailable
            });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user == null) return RedirectToPage("/Account/Login");

            // Reload data and recalculate shipping
            await LoadCartData(user.Id);
            if (CartItems == null || !CartItems.Any())
            {
                ModelState.AddModelError("", "Votre panier est vide.");
                await OnGetAsync();
                return Page();
            }

            if (SelectedAddressId == 0)
            {
                ModelState.AddModelError("", "Veuillez sélectionner une adresse.");
                await OnGetAsync();
                return Page();
            }

            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == SelectedAddressId && a.UserId == user.Id);
            if (address == null)
            {
                ModelState.AddModelError("", "Adresse invalide.");
                await OnGetAsync();
                return Page();
            }

            await CalculateShippingFees(CartItems, address.Id);

            if (!ShippingAvailable)
            {
                ModelState.AddModelError("", "Impossible de passer la commande : au moins une livraison indisponible.");
                await OnGetAsync();
                return Page();
            }

            var orderProducts = CartItems.Select(i => new OrderProduct
            {
                ProductVariantId = i.ProductVariantId,
                Quantity         = i.Quantity,
                PriceAtOrder     = CalculateUnitPriceWithBreaks(i.ProductVariant, i.Quantity),
                DeliveryFee      = DeliveryFeeByVariantId.GetValueOrDefault(i.ProductVariantId),
                CompanyId        = i.ProductVariant.Product.CompanyId
            }).ToList();

            var order = new Order
            {
                UserId        = user.Id,
                // OrderDate supprimé - remplacï¿½ par CreatedAt de BaseEntity
                TotalAmount   = orderProducts.Sum(p => p.PriceAtOrder * p.Quantity),
                ShippingTotal = ShippingTotal,
                AddressId     = SelectedAddressId,
                Status        = OrderStatus.Pending,
                Products      = orderProducts
            };
            await SaveOrder(order, await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == user.Id));

            return RedirectToPage("/Payments/Payment", new { type = "order", id = order.Id });
        }

        public async Task<IActionResult> OnPostRemoveCartItemAsync(int productVariantId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return new JsonResult(new { success = false, message = "Vous devez etre connecte." });
            }

            if (productVariantId <= 0)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return new JsonResult(new { success = false, message = "Produit invalide." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var item = cart?.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
            if (cart == null || item == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return new JsonResult(new { success = false, message = "Ce produit n'est plus dans votre panier." });
            }

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();

            var remainingItems = cart.Items.Count - 1;
            return new JsonResult(new
            {
                success = true,
                message = "Le produit a ete retire du panier.",
                cartIsEmpty = remainingItems <= 0,
                redirect = remainingItems <= 0 ? "/MyCart" : null
            });
        }

        private async Task LoadCartData(string userId)
        {
            // OPTIMISÉ: Projection SELECT au lieu d'Include en cascade
            var cart = await _context.Carts
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => new Cart
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    Items = c.Items.Select(i => new CartItem
                    {
                        Id = i.Id,
                        CartId = i.CartId,
                        ProductVariantId = i.ProductVariantId,
                        Quantity = i.Quantity,
                        PriceSnapshot = i.PriceSnapshot,
                        ProductVariant = new ProductVariant
                        {
                            Id = i.ProductVariant.Id,
                            Price = i.ProductVariant.Price,
                            Sku = i.ProductVariant.Sku,
                            ProductId = i.ProductVariant.ProductId,
                            Product = new Product
                            {
                                Id = i.ProductVariant.Product.Id,
                                Name = i.ProductVariant.Product.Name,
                                CompanyId = i.ProductVariant.Product.CompanyId
                            },
                            PriceBreaks = i.ProductVariant.PriceBreaks
                                .Where(pb => pb.IsActive && !pb.IsDeleted)
                                .ToList(),
                            ShippingPackageType = i.ProductVariant.ShippingPackageType,
                            WeightKg = i.ProductVariant.WeightKg,
                            RequiresSpecialHandling = i.ProductVariant.RequiresSpecialHandling,
                            ShippingQuoteRequired = i.ProductVariant.ShippingQuoteRequired
                        }
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            CartItems = cart?.Items.ToList() ?? new List<CartItem>();
            
            // Au checkout, le prix final est recalculé depuis les paliers actuels.
            SubTotal = CartItems.Sum(i => CalculateItemPriceWithBreaks(i.ProductVariant, i.Quantity));
            CartId = cart?.Id ?? 0;
        }

        private async Task LoadAddressData(string userId)
        {
            // OPTIMISÉ: Projection SELECT + AsNoTracking
            UserAddresses = await _context.Addresses
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new Address
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    Line1 = a.Line1,
                    Line2 = a.Line2,
                    CountryId = a.CountryId,
                    StateId = a.StateId,
                    CityId = a.CityId,
                    NeighborhoodId = a.NeighborhoodId,
                    IsDefault = a.IsDefault,
                    Country = a.Country != null ? new Country { Id = a.Country.Id, Name = a.Country.Name } : null,
                    State = a.State != null ? new State { Id = a.State.Id, Name = a.State.Name } : null,
                    City = a.City != null ? new City { Id = a.City.Id, Name = a.City.Name } : null
                })
                .ToListAsync();
        }

        private async Task CalculateShippingFees(List<CartItem> items, int addressId)
        {
            ShippingFees.Clear();
            DeliveryFeeByVariantId.Clear();
            ShippingTotal = 0;
            ShippingAvailable = true;

            var address = await _context.Addresses.FindAsync(addressId);
            if (address == null)
            {
                ShippingAvailable = false;
                return;
            }

            var groups = items.GroupBy(i => i.ProductVariant.Product.CompanyId);
            var companies = await _context.Companies
                .Where(c => groups.Select(g => g.Key).Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            foreach (var g in groups)
            {
                var result = await CalculateShippingFeeForCompanyGroup(g.Key, g.ToList(), address);
                var feeValue = result.Amount;
                var found = result.IsAvailable;

                if (!found) ShippingAvailable = false;
                if (result.QuoteRequired) ShippingAvailable = false;

                ShippingFees.Add(new ShippingFeeDisplay
                {
                    CompanyId = g.Key,
                    CompanyName = companies.GetValueOrDefault(g.Key, "Entreprise inconnue"),
                    Amount = feeValue,
                    IsAvailable = found,
                    QuoteRequired = result.QuoteRequired,
                    Message = result.Message,
                    Lines = result.Lines
                });

                foreach (var line in result.Lines)
                {
                    DeliveryFeeByVariantId[line.ProductVariantId] = line.Amount;
                }

                ShippingTotal += feeValue;
            }
        }

        private async Task<CompanyShippingCalculation> CalculateShippingFeeForCompanyGroup(int companyId, List<CartItem> items, Address address)
        {
            var result = new CompanyShippingCalculation();
            var fees = await _context.ShippingFees
                .Where(sf => sf.CompanyId == companyId && !sf.IsDeleted && sf.IsActive)
                .ToListAsync();

            if (!fees.Any())
            {
                result.Message = "Aucun tarif de livraison configure pour ce vendeur.";
                return result;
            }

            var calculatedLines = new List<ShippingLineCalculation>();

            foreach (var item in items)
            {
                var variant = item.ProductVariant;
                var packageType = variant.ShippingQuoteRequired
                    ? ShippingPackageType.QuoteRequired
                    : variant.ShippingPackageType;

                if (packageType == ShippingPackageType.QuoteRequired)
                {
                    result.QuoteRequired = true;
                    result.Message = "Un ou plusieurs produits necessitent une confirmation manuelle des frais de livraison.";
                    calculatedLines.Add(new ShippingLineCalculation
                    {
                        ProductVariantId = item.ProductVariantId,
                        ProductName = variant.Product?.Name ?? "Produit",
                        Quantity = item.Quantity,
                        PackageType = packageType,
                        FullCost = 0,
                        AdditionalCost = 0,
                        Explanation = "Sur devis",
                        IsBlocking = true,
                        ActionHint = "Ce produit necessite une confirmation manuelle. Retirez-le de cette commande ou contactez le vendeur avant de payer."
                    });
                    continue;
                }

                var fee = FindBestShippingFee(fees, address, packageType);
                if (fee == null)
                {
                    result.Message = $"Livraison indisponible pour {variant.Product?.Name ?? "un produit"} vers cette adresse.";
                    calculatedLines.Add(new ShippingLineCalculation
                    {
                        ProductVariantId = item.ProductVariantId,
                        ProductName = variant.Product?.Name ?? "Produit",
                        Quantity = item.Quantity,
                        PackageType = packageType,
                        FullCost = 0,
                        AdditionalCost = 0,
                        Explanation = "Aucun tarif correspondant",
                        IsBlocking = true,
                        ActionHint = "Retirez ce produit du panier ou essayez une autre adresse de livraison."
                    });
                    continue;
                }

                if (fee.QuoteRequired)
                {
                    result.QuoteRequired = true;
                    result.Message = "Le vendeur doit confirmer les frais de livraison pour ce panier.";
                }

                var basePrice = fee.BasePrice > 0 ? fee.BasePrice : fee.Price;
                var weightFee = (fee.PricePerKg ?? 0m) * (variant.WeightKg ?? 0m) * item.Quantity;
                var extraUnits = Math.Max(0, item.Quantity - 1);
                var fullCost = basePrice + weightFee + (fee.AdditionalUnitPrice * extraUnits);
                fullCost = ClampShippingAmount(fullCost, fee.MinPrice, fee.MaxPrice);

                var additionalCost = fee.AdditionalItemPrice + weightFee + (fee.AdditionalUnitPrice * extraUnits);

                calculatedLines.Add(new ShippingLineCalculation
                {
                    ProductVariantId = item.ProductVariantId,
                    ProductName = variant.Product?.Name ?? "Produit",
                    Quantity = item.Quantity,
                    PackageType = packageType,
                    Fee = fee,
                    FullCost = fullCost,
                    AdditionalCost = additionalCost,
                    Explanation = fee.PackageType == ShippingPackageType.Any
                        ? "Tarif general du vendeur"
                        : $"Tarif {packageType}",
                    IsBlocking = fee.QuoteRequired,
                    ActionHint = fee.QuoteRequired
                        ? "Ce tarif est sur devis. Retirez ce produit de cette commande ou contactez le vendeur."
                        : string.Empty
                });
            }

            if (result.QuoteRequired)
            {
                result.Lines = calculatedLines.Select(l => ToDisplayLine(l, false, l.AdditionalCost)).ToList();
                return result;
            }

            if (calculatedLines.Any(l => l.Fee == null))
            {
                result.Lines = calculatedLines.Select(l => ToDisplayLine(l, false, 0)).ToList();
                return result;
            }

            var primary = calculatedLines
                .OrderByDescending(l => l.FullCost)
                .ThenByDescending(l => l.Quantity)
                .First();

            var rawLineAmounts = calculatedLines.ToDictionary(
                l => l,
                l => l.ProductVariantId == primary.ProductVariantId ? l.FullCost : l.AdditionalCost);

            var rawTotal = rawLineAmounts.Values.Sum();
            var finalTotal = primary.Fee != null
                ? ClampShippingAmount(rawTotal, primary.Fee.MinPrice, primary.Fee.MaxPrice)
                : rawTotal;

            var factor = rawTotal > 0 && finalTotal < rawTotal ? finalTotal / rawTotal : 1m;

            result.IsAvailable = true;
            result.Amount = Math.Round(finalTotal, 2);
            result.Message = "Livraison calculee par vendeur avec un frais principal et des supplements reduits.";
            result.Lines = rawLineAmounts
                .Select(kvp => ToDisplayLine(kvp.Key, kvp.Key.ProductVariantId == primary.ProductVariantId, Math.Round(kvp.Value * factor, 2)))
                .ToList();

            return result;
        }

        private static ShippingFee FindBestShippingFee(IEnumerable<ShippingFee> fees, Address address, ShippingPackageType packageType)
        {
            return fees
                .Where(f => f.CountryId == address.CountryId
                    && f.StateId == address.StateId
                    && f.CityId == address.CityId
                    && (f.NeighborhoodId == null || f.NeighborhoodId == address.NeighborhoodId)
                    && (f.PackageType == packageType || f.PackageType == ShippingPackageType.Any))
                .OrderByDescending(f => f.PackageType == packageType ? 10 : 0)
                .ThenByDescending(f => f.NeighborhoodId == address.NeighborhoodId ? 5 : 0)
                .FirstOrDefault();
        }

        private static decimal ClampShippingAmount(decimal amount, decimal? min, decimal? max)
        {
            if (min.HasValue && amount < min.Value) amount = min.Value;
            if (max.HasValue && max.Value > 0 && amount > max.Value) amount = max.Value;
            return amount;
        }

        private static ShippingFeeLineDisplay ToDisplayLine(ShippingLineCalculation line, bool isPrimary, decimal amount)
        {
            var isBlocking = line.IsBlocking || line.Fee == null || line.PackageType == ShippingPackageType.QuoteRequired;
            return new ShippingFeeLineDisplay
            {
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                PackageType = line.PackageType,
                Amount = amount,
                IsPrimary = isPrimary,
                IsBlocking = isBlocking,
                CanRemoveFromCart = isBlocking,
                Explanation = isPrimary ? "Produit principal de livraison" : line.Explanation,
                ActionHint = string.IsNullOrWhiteSpace(line.ActionHint)
                    ? (isBlocking ? "Retirez ce produit du panier ou essayez une autre adresse de livraison." : string.Empty)
                    : line.ActionHint
            };
        }

        private class CompanyShippingCalculation
        {
            public decimal Amount { get; set; }
            public bool IsAvailable { get; set; }
            public bool QuoteRequired { get; set; }
            public string Message { get; set; }
            public List<ShippingFeeLineDisplay> Lines { get; set; } = new();
        }

        private class ShippingLineCalculation
        {
            public int ProductVariantId { get; set; }
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public ShippingPackageType PackageType { get; set; }
            public ShippingFee Fee { get; set; }
            public decimal FullCost { get; set; }
            public decimal AdditionalCost { get; set; }
            public string Explanation { get; set; }
            public bool IsBlocking { get; set; }
            public string ActionHint { get; set; }
        }

        // IMPORTANT: Avec NpgsqlRetryingExecutionStrategy, toute transaction utilisateur doit
        // être exécutée via CreateExecutionStrategy().ExecuteAsync pour rester compatible avec le retry.
        private async Task SaveOrder(Order order, Cart cart)
        {
            // IMPORTANT: Avec NpgsqlRetryingExecutionStrategy, toute transaction utilisateur doit
            // être exécutée via CreateExecutionStrategy().ExecuteAsync pour permettre les retries
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync(); // Sauvegarder d'abord pour obtenir l'ID

                    // INTÉGRATION AMBASSADEUR: Associer la commande à l'ambassadeur si applicable
                    try
                    {
                        var ambassadorAssociated = await _ambassadorIntegration.AssociateOrderWithAmbassadorAsync(order, HttpContext);
                        if (ambassadorAssociated)
                        {
                            _logger.LogInformation($"Order {order.Id} successfully associated with ambassador");
                            await _context.SaveChangesAsync(); // Sauvegarder l'association ambassadeur
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to associate order {order.Id} with ambassador, continuing without ambassador");
                        // Ne pas faire échouer la commande si l'association ambassadeur échoue
                    }

                    _context.CartItems.RemoveRange(cart.Items);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Order {order.Id} saved successfully with total {order.TotalAmount:C}");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Failed to save order for user {order.UserId}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Calcule le prix total d'un item en fonction des paliers de prix
        /// </summary>
        private decimal CalculateItemPriceWithBreaks(ProductVariant variant, int quantity)
        {
            if (variant.PriceBreaks?.Any() != true || quantity <= 0)
            {
                return quantity * variant.Price;
            }

            var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, quantity);
            return quantity * (applicablePriceBreak?.UnitPrice ?? variant.Price);
        }

        /// <summary>
        /// Calcule le prix unitaire en fonction des paliers de prix
        /// </summary>
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
            return CalculateUnitPriceWithBreaks(item.ProductVariant, item.Quantity);
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
