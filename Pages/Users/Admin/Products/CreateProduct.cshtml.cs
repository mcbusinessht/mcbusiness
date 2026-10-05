using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Products
{
    public class CreateProductModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateProductModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string ProductName { get; set; }
    [BindProperty]
    public string Color { get; set; }
    [BindProperty]
    public string Storage { get; set; }
    [BindProperty]
    public string Variant1Sku { get; set; }
    [BindProperty]
    public string Variant2Sku { get; set; }

    public IActionResult OnPost()
    {
        using (var context = _context)
        {
            // 1. Create Product
            var product = new Product { Name = ProductName };
            context.Products.Add(product);
            context.SaveChanges();

            // 2. Create Characteristics (Color, Storage)
            var color = new Characteristic { Name = "Color" };
            var storage = new Characteristic { Name = "Storage" };
            context.Characteristics.AddRange(color, storage);
            context.SaveChanges();

            // 3. Create Characteristic Values
            var black = new CharacteristicValue { CharacteristicId = color.Id, Value = Color };
            var red = new CharacteristicValue { CharacteristicId = color.Id, Value = "Red" };
            var gb128 = new CharacteristicValue { CharacteristicId = storage.Id, Value = Storage };
            var gb256 = new CharacteristicValue { CharacteristicId = storage.Id, Value = "256GB" };
            context.CharacteristicValues.AddRange(black, red, gb128, gb256);
            context.SaveChanges();

            // 4. Create Product Variants
            var variant1 = new ProductVariant { ProductId = product.Id, Sku = Variant1Sku };
            var variant2 = new ProductVariant { ProductId = product.Id, Sku = Variant2Sku };
            context.ProductVariants.AddRange(variant1, variant2);
            context.SaveChanges();

            // 5. Link Characteristics to Variants
            context.ProductVariantCharacteristics.Add(new ProductVariantCharacteristic { ProductVariantId = variant1.Id, CharacteristicValueId = black.Id });
            context.ProductVariantCharacteristics.Add(new ProductVariantCharacteristic { ProductVariantId = variant1.Id, CharacteristicValueId = gb128.Id });
            context.ProductVariantCharacteristics.Add(new ProductVariantCharacteristic { ProductVariantId = variant2.Id, CharacteristicValueId = red.Id });
            context.ProductVariantCharacteristics.Add(new ProductVariantCharacteristic { ProductVariantId = variant2.Id, CharacteristicValueId = gb256.Id });
            context.SaveChanges();

            // 6. Add Product Images
            context.ProductImages.Add(new ProductImage { ProductVariantId = variant1.Id, Url = "img_black_128.jpg" });
            context.ProductImages.Add(new ProductImage { ProductVariantId = variant2.Id, Url = "img_red_256.jpg" });
            context.SaveChanges();

            // 7. Add Stock
            context.Stocks.Add(new Stock { ProductVariantId = variant1.Id, Quantity = 50 });
            context.Stocks.Add(new Stock { ProductVariantId = variant2.Id, Quantity = 30 });
            context.SaveChanges();

            // 8. Create Cart (anonymous)
            var cart = new Cart();
            context.Carts.Add(cart);
            context.SaveChanges();

            // 9. Add items to cart
            context.CartItems.Add(new CartItem { CartId = cart.Id, ProductVariantId = variant1.Id, Quantity = 1 });
            context.SaveChanges();
        }

        return RedirectToPage("/Index");
    }
}
}
