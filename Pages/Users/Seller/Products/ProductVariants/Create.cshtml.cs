using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Seller.Products.ProductVariants
{
    public class CreateModel : PageModel                        
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ProductVariant ProductVariant { get; set; } = new();

        [BindProperty]
        public List<ProductVariantCharacteristic> ProductVariantCharacteristics { get; set; } = new();

        [BindProperty]
        public List<PriceBreak> PriceBreaks { get; set; } = new();

        [BindProperty]   
        public string SelectedCharacteristics { get; set; } = string.Empty;

        public Product Product { get; set; }
        public Company Company { get; set; }
        public ApplicationUser CurrentUser { get; set; }

        public IActionResult OnGet(int? id)
        {
            // 1) s?curit? utilisateur / entreprise
            CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (CurrentUser == null) return Redirect("/Index");

            Company = _context.Companies.FirstOrDefault(c => c.SellerId == CurrentUser.Id);
            if (Company == null) return Redirect("/Users/Seller/Index");

            // 2) toujours remplir la liste des produits du Seller
            var products = _context.Products
                            .Where(p => p.CompanyId == Company.Id)
                            .ToList();
            ViewData["ProductId"] = new SelectList(products, "Id", "Name");

            // 3) si un id de produit est fourni et valide, charger ce produit
            IEnumerable<Characteristic> characteristics = Enumerable.Empty<Characteristic>();

            characteristics = _context.Characteristics.ToList();
            if (id.HasValue && id.Value > 0)
            {
                Product = _context.Products.FirstOrDefault(p => p.Id == id.Value);
                if (Product == null)
                {
                    // produit invalide ? retour ? l?index des variants
                    return Redirect("/Users/Seller/Products/ProductVariants/Index");
                }

                // pr?-s?lection du produit dans le dropdown
                ViewData["ProductId"] = new SelectList(products.Where(p=>p.Id == id), "Id", "Name");  

                // ne proposer que les caract?ristiques de la cat?gorie de ce produit
                characteristics = characteristics.Where(c => c.CategoryId == Product.CategoryId)
                                    .ToList();
            }

            // 4) placer la liste (?ventuellement vide) dans le ViewData pour le <select>
            ViewData["Characteristic"] = new SelectList(characteristics, "Id", "Name");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // repopuler ViewData en cas d?erreur
                await RepopulateViewData();
                return Page();
            }

            var product = _context.Products.FirstOrDefault(p => p.Id == ProductVariant.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("", "Produit introuvable.");
                await RepopulateViewData();
                return Page();
            }

            // G?n?ration du SKU
            ProductVariant.Sku = GenerateSKU(product.Id, product.Name);
            ProductVariant.PriceSnapshot = ProductVariant.Price;

            _context.ProductVariants.Add(ProductVariant);
            await _context.SaveChangesAsync();

            // Cr?ation du stock initial
            _context.Stocks.Add(new Stock {
                ProductVariantId = ProductVariant.Id,
                Quantity = ProductVariant.Quantity
            });
            await _context.SaveChangesAsync();

            // Gestion des caract?ristiques s?lectionn?es
            if (!string.IsNullOrEmpty(SelectedCharacteristics))
            {
                var pairs = SelectedCharacteristics.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var ids = pair.Split('-');
                    ProductVariantCharacteristics.Add(new ProductVariantCharacteristic {
                        ProductVariantId      = ProductVariant.Id,
                        CharacteristicId      = int.Parse(ids[0]),
                        CharacteristicValueId = int.Parse(ids[1])
                    });
                }
                _context.ProductVariantCharacteristics.AddRange(ProductVariantCharacteristics);
                await _context.SaveChangesAsync();
            }

            // Gestion des paliers de prix (PriceBreaks)
            if (PriceBreaks?.Any() == true)
            {
                var validPriceBreaks = PriceBreaks
                    .Where(pb => pb.MinQuantity > 0 && pb.UnitPrice > 0)
                    .ToList();

                foreach (var priceBreak in validPriceBreaks)
                {
                    priceBreak.ProductVariantId = ProductVariant.Id;
                    priceBreak.IsActive = true;
                    priceBreak.IsDeleted = false;
                    
                    // Calculer le pourcentage de r?duction par rapport au prix de base
                    if (ProductVariant.Price > 0 && priceBreak.UnitPrice < ProductVariant.Price)
                    {
                        priceBreak.DiscountPercentage = Math.Round(
                            ((ProductVariant.Price - priceBreak.UnitPrice) / ProductVariant.Price) * 100, 2);
                    }
                }

                _context.PriceBreaks.AddRange(validPriceBreaks);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("../ProductImages/Create", new { id = ProductVariant.Id });
        }

        private async Task RepopulateViewData()
        {
            // m?me logique que OnGet pour repopuler les dropdowns si Post ?choue
            CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            Company = _context.Companies.FirstOrDefault(c => c.SellerId == CurrentUser.Id);
            var products = _context.Products.Where(p => p.CompanyId == Company.Id).ToList();
            ViewData["ProductId"] = new SelectList(products, "Id", "Name", ProductVariant.ProductId);

            // caract?ristiques de la cat?gorie du produit choisi
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == ProductVariant.ProductId);
            var chars = product != null
                ? _context.Characteristics.Where(c => c.CategoryId == product.CategoryId).ToList()
                : new List<Characteristic>();
            ViewData["Characteristic"] = new SelectList(chars, "Id", "Name");
        }

        public string GenerateSKU(int productId, string productName)
        {
            var baseName = productName.Replace(" ", "-");
            string code;
            string sku;
            do
            {
                code = Guid.NewGuid().ToString("N").Substring(0, 4);
                sku = $"{baseName}-{productId}-{code}";
            }
            while (_context.ProductVariants.Any(pv => pv.Sku == sku));
            return sku;
        }

        // Handler AJAX pour r?cup?rer les caract?ristiques d?un produit
        public async Task<JsonResult> OnGetCharacteristicsByProduct(int productId)
        {
            var product = await _context.Products
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null) return new JsonResult(Array.Empty<object>());

            var chars = await _context.Characteristics
                               .Where(c => c.CategoryId == product.CategoryId)
                               .Select(c => new { c.Id, c.Name })
                               .ToListAsync();
            return new JsonResult(chars);
        }

        // Handler AJAX pour r?cup?rer les valeurs d?une caract?ristique
        public JsonResult OnGetCharacteristicValues(int characteristicId)
        {
            var values = _context.CharacteristicValues
                          .Where(v => v.CharacteristicId == characteristicId)
                          .Select(v => new { v.Id, v.Value })
                          .ToList();
            return new JsonResult(values);
        }
    }
}
