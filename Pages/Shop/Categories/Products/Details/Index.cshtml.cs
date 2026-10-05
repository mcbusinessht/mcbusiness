using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using MCBusiness.Services;

namespace MCBusiness.Pages.Shop.Categories.Products.Details
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public Product Product { get; set; }
        public string VariantsJson { get; set; }
        public Dictionary<string, List<string>> Characteristics { get; set; } = new();
        public List<ProductImage> AllImages { get; set; } = new();
        public ApplicationUser CurrentUser { get; private set; }
        public double AverageRating { get; set; }
        public List<ProductReview> Reviews { get; set; } = new();

        [BindProperty]
        public ProductReview NewReview { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Support des deux formats: /Details/2 et /Details?id=2
            if (!Id.HasValue && int.TryParse(Request.Query["id"], out int queryId))
            {
                Id = queryId;
            }

            if (!Id.HasValue) return Redirect("/Shop");

            if (User.Identity.IsAuthenticated)
            {
                CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            }

            Product = await _context.Products
                .Include(p=>p.Company)
                .ThenInclude(p=>p.Seller)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Currency)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.ProductVariantCharacteristics)
                        .ThenInclude(pvc => pvc.CharacteristicValue)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.ProductVariantCharacteristics)
                        .ThenInclude(pvc => pvc.Characteristic)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Images)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Stock)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.PriceBreaks)
                .WhereSellableProducts()
                .FirstOrDefaultAsync(p => p.Id == Id.Value);

            if (Product == null) return NotFound();
            Product.ProductView += 1;
            await _context.SaveChangesAsync();


            // Préparer les données pour le frontend
            var availableVariants = Product.Variants
                .Where(ProductVisibilityFilters.IsSellableVariant)
                .ToList();

            if (!availableVariants.Any())
            {
                return NotFound();
            }

            var variantsData = availableVariants.Select(v => new
            {
                Id = v.Id,
                Sku = v.Sku,
                Price = v.Price,
                PriceSnapshot = v.PriceSnapshot,
                CurrencyCode = v.Currency?.Code,
                CurrencyBase = v.CurrencyBase,
                CurrencySymbol = v.Currency?.Symbol ?? v.CurrencyBase.ToString(),   
                IsAvailable = v.IsAvailable,
                Quantity = v.Quantity,
                StockQuantity = v.Stock != null && !v.Stock.IsDeleted ? v.Stock.Quantity : (int?)null,
                ShippingPackageType = v.ShippingPackageType.ToString(),
                WeightKg = v.WeightKg,
                RequiresSpecialHandling = v.RequiresSpecialHandling,
                ShippingQuoteRequired = v.ShippingQuoteRequired,
                Images = v.Images.Select(i => new { Url = i.Url }).ToList(),
                Characteristics = v.ProductVariantCharacteristics
                    .GroupBy(pvc => pvc.Characteristic.Name)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().CharacteristicValue.Value 
                    ),
                PriceBreaks = v.PriceBreaks
                    .Where(pb => !pb.IsDeleted && pb.IsActive)
                    .OrderBy(pb => pb.MinQuantity)
                    .Select(pb => new {
                        MinQuantity = pb.MinQuantity,
                        MaxQuantity = pb.MaxQuantity,
                        UnitPrice = pb.UnitPrice,
                        DiscountPercentage = pb.DiscountPercentage,
                        DisplayLabel = pb.DisplayLabel,
                        IsActive = pb.IsActive
                    }).ToList()
            }).ToList();

            VariantsJson = JsonSerializer.Serialize(variantsData);

            // Récupérer toutes les images pour l'affichage initial
            var availableVariantIds = availableVariants.Select(v => v.Id).ToList();

            AllImages = await _context.ProductImages
                .AsNoTracking()
                .Where(img => availableVariantIds.Contains(img.ProductVariantId))
                .ToListAsync();

            // Extraire les caractéristiques uniques avec leurs valeurs
            Characteristics = availableVariants
                .SelectMany(v => v.ProductVariantCharacteristics)
                .GroupBy(pvc => pvc.Characteristic.Name)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(pvc => pvc.CharacteristicValue.Value)
                          .Distinct()
                          .OrderBy(v => v)
                          .ToList()
                );

            await LoadReviews(Id.Value);
            CalculateAverageRating();

            return Page();
        }

        
        private void CalculateAverageRating()
        {
            if (Reviews.Any())
            {
                AverageRating = Reviews.Average(r => r.Rate);
            }
        }

        public async Task<IActionResult> OnPostAddReviewAsync()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl = Request.Path });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            NewReview.UserId = userId;
            NewReview.DateReview = DateTimeOffset.UtcNow;

            if (!ModelState.IsValid)
            {
                await LoadProductData(NewReview.ProductId);
                await LoadReviews(NewReview.ProductId);
                CalculateAverageRating();
                return Page();
            }

            _context.ProductReviews.Add(NewReview);
            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Votre avis a été publié avec succès !";
            return RedirectToPage(new { id = NewReview.ProductId });
        }
        private async Task LoadProductData(int productId)
        {
            CurrentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            Product = await _context.Products
                .Include(p => p.Company)
                .ThenInclude(p => p.Seller)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Currency)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.ProductVariantCharacteristics)
                        .ThenInclude(pvc => pvc.CharacteristicValue)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.ProductVariantCharacteristics)
                        .ThenInclude(pvc => pvc.Characteristic)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Images)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Stock)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.PriceBreaks)
                .WhereSellableProducts()
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (Product != null)
            {
                var availableVariants = Product.Variants
                    .Where(ProductVisibilityFilters.IsSellableVariant)
                    .ToList();

                VariantsJson = JsonSerializer.Serialize(availableVariants.Select(v => new
                {
                    Id = v.Id,
                    Sku = v.Sku,
                    Price = v.Price,
                    PriceSnapshot = v.PriceSnapshot,
                    CurrencyCode = v.Currency?.Code,
                    CurrencyBase = v.CurrencyBase,
                    CurrencySymbol = v.Currency?.Symbol ?? v.CurrencyBase.ToString(),
                    IsAvailable = v.IsAvailable,
                    Quantity = v.Quantity,
                    StockQuantity = v.Stock != null && !v.Stock.IsDeleted ? v.Stock.Quantity : (int?)null,
                    ShippingPackageType = v.ShippingPackageType.ToString(),
                    WeightKg = v.WeightKg,
                    RequiresSpecialHandling = v.RequiresSpecialHandling,
                    ShippingQuoteRequired = v.ShippingQuoteRequired,
                    Images = v.Images.Select(i => new { Url = i.Url }).ToList(),
                    Characteristics = v.ProductVariantCharacteristics
                        .GroupBy(pvc => pvc.Characteristic.Name)
                        .ToDictionary(
                            g => g.Key,
                            g => g.First().CharacteristicValue.Value
                        ),
                    PriceBreaks = v.PriceBreaks
                        .Where(pb => !pb.IsDeleted && pb.IsActive)
                        .OrderBy(pb => pb.MinQuantity)
                        .Select(pb => new {
                            MinQuantity = pb.MinQuantity,
                            MaxQuantity = pb.MaxQuantity,
                            UnitPrice = pb.UnitPrice,
                            DiscountPercentage = pb.DiscountPercentage,
                            DisplayLabel = pb.DisplayLabel,
                            IsActive = pb.IsActive
                        }).ToList()
                }).ToList());

                var availableVariantIds = availableVariants.Select(v => v.Id).ToList();

                AllImages = await _context.ProductImages
                    .AsNoTracking()
                    .Where(img => availableVariantIds.Contains(img.ProductVariantId))
                    .ToListAsync();

                Characteristics = availableVariants
                    .SelectMany(v => v.ProductVariantCharacteristics)
                    .GroupBy(pvc => pvc.Characteristic.Name)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(pvc => pvc.CharacteristicValue.Value)
                              .Distinct()
                              .OrderBy(v => v)
                              .ToList()
                    );
            }
        }


         private async Task LoadReviews(int productId)
        {
            Reviews = await _context.ProductReviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.DateReview)
                .Take(10)
                .ToListAsync();
        }
        public async Task<IActionResult> OnPostAddToCartAsync(int variantId, int quantity = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userId))
            {
                // Vérifier si c'est une requête AJAX
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                    Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    return new JsonResult(new { success = false, message = "Vous devez être connecté pour ajouter au panier", redirect = "/Account/Login" });
                }
                return RedirectToPage("/Account/Login", new { returnUrl = Request.Path });
            }

            try
            {
                var variant = await _context.ProductVariants
                    .Include(v => v.Product)
                        .ThenInclude(p => p.Category)
                    .Include(v => v.Currency)
                    .Include(v => v.Stock)
                    .Include(v => v.PriceBreaks)
                    .WhereSellableVariants()
                    .FirstOrDefaultAsync(v => v.Id == variantId);

                if (variant == null) 
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                        Request.ContentType?.Contains("multipart/form-data") == true)
                    {
                        return new JsonResult(new { success = false, message = "Produit non trouvé" });
                    }
                    return NotFound();
                }

                if (quantity <= 0)
                {
                    var message = "La quantité doit être supérieure à zéro";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                        Request.ContentType?.Contains("multipart/form-data") == true)
                    {
                        return new JsonResult(new { success = false, message });
                    }
                    TempData["ErrorMessage"] = message;
                    return RedirectToPage();
                }

                var availableStock = variant.Stock != null && !variant.Stock.IsDeleted
                    ? variant.Stock.Quantity
                    : variant.Quantity;

                if (!variant.IsAvailable || availableStock <= 0)
                {
                    var message = "Ce produit n'est pas disponible";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                        Request.ContentType?.Contains("multipart/form-data") == true)
                    {
                        return new JsonResult(new { success = false, message });
                    }
                    TempData["ErrorMessage"] = message;
                    return RedirectToPage();
                }

                if (quantity > availableStock)
                {
                    var message = $"Quantité disponible insuffisante (max: {availableStock})";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                        Request.ContentType?.Contains("multipart/form-data") == true)
                    {
                        return new JsonResult(new { success = false, message });
                    }
                    TempData["ErrorMessage"] = message;
                    return RedirectToPage();
                }

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        Items = new List<CartItem>()
                    };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                cart.Items ??= new List<CartItem>();
                var existingCartItem = cart.Items.FirstOrDefault(i => i.ProductVariantId == variantId);
                var currentCartQuantity = existingCartItem?.Quantity ?? 0;
                var totalRequestedQuantity = currentCartQuantity + quantity;

                if (totalRequestedQuantity > availableStock)
                {
                    var availableToAdd = availableStock - currentCartQuantity;
                    var message = availableToAdd <= 0 
                        ? "Ce produit est déjà au maximum dans votre panier"
                        : $"Stock insuffisant. Vous pouvez ajouter au maximum {availableToAdd} unité(s) supplémentaire(s). Stock disponible: {availableStock}";
                    
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                        Request.ContentType?.Contains("multipart/form-data") == true)
                    {
                        return new JsonResult(new { success = false, message });
                    }
                    TempData["ErrorMessage"] = message;
                    return RedirectToPage();
                }

                if (existingCartItem != null)
                {
                    existingCartItem.Quantity = totalRequestedQuantity;
                    existingCartItem.UpdatedAt = DateTimeOffset.UtcNow;
                    
                    // Recalculer le prix basé sur la nouvelle quantité totale avec PriceBreaks
                    var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, totalRequestedQuantity);
                    existingCartItem.PriceSnapshot = applicablePriceBreak?.UnitPrice ?? variant.Price;
                }
                else
                {
                    // Calculer le prix basé sur les paliers pour la quantité demandée
                    var applicablePriceBreak = FindApplicablePriceBreak(variant.PriceBreaks, quantity);
                    var unitPrice = applicablePriceBreak?.UnitPrice ?? variant.Price;

                    var newCartItem = new CartItem
                    {
                        ProductVariantId = variantId,
                        Quantity = quantity,
                        PriceSnapshot = unitPrice,
                        AddedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        CartId = cart.Id
                    };
                    
                    _context.CartItems.Add(newCartItem);
                }

                await _context.SaveChangesAsync();

                var successMessage = $"{variant.Product.Name} a été ajouté à votre panier ({quantity} unité{(quantity > 1 ? "s" : "")})!";
                
                // Réponse pour requêtes AJAX
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                    Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    return new JsonResult(new { 
                        success = true, 
                        message = successMessage,
                        productName = variant.Product.Name,
                        quantity = quantity,
                        price = variant.Price,
                        currency = variant.Currency?.Symbol ?? "HTG"
                    });
                }

                TempData["SuccessMessage"] = successMessage;
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'ajout au panier - Variant: {VariantId}, Quantity: {Quantity}, User: {UserId}", 
                    variantId, quantity, userId);
                
                var errorMessage = "Une erreur est survenue lors de l'ajout au panier. Veuillez réessayer.";
                
                // Réponse pour requêtes AJAX
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                    Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    return new JsonResult(new { success = false, message = errorMessage });
                }
                
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToPage();
            }
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
