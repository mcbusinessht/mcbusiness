using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.IdentityModel.Tokens;

namespace MCBusiness.Pages.Users.Admin.Sellers.Companies
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Company Company { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var company = await _context.Companies
                .Include(m => m.Seller)
                .Include(m => m.Addresses.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.Country)
                .Include(m => m.Addresses.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.State)
                .Include(m => m.Addresses.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.City)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (company == null)
            {
                return NotFound();
            }
            else
            {
                Company = company;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var company = await _context.Companies.FindAsync(id);
            if (company != null)
            {
                var product = await _context.Products.Where(p => p.CompanyId == company.Id).ToListAsync();
                if (!product.IsNullOrEmpty())
                {
                    foreach (var pr in product)
                    {
                        var ProductVariant = await _context.ProductVariants.Where(p => p.ProductId == pr.Id).ToListAsync();
                        if (!ProductVariant.IsNullOrEmpty())
                        {
                            foreach (var item in ProductVariant)
                            {
                                var Images = await _context.ProductImages.Where(c => c.ProductVariantId == item.Id).ToListAsync();
                                if (!Images.IsNullOrEmpty())
                                {
                                    foreach (var el in Images)
                                    {
                                        _context.ProductImages.Remove(el);
                                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", el.Url.TrimStart('/'));
                                        if (System.IO.File.Exists(oldFilePath))
                                        {
                                            try
                                            {
                                                System.IO.File.Delete(oldFilePath);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.Error.WriteLine($"Erreur lors de la suppression de l'ancienne image : {ex.Message}");
                                            }
                                        }

                                    }
                                    await _context.SaveChangesAsync();
                                }
                                var ProductVariantCharacteristic = await _context.ProductVariantCharacteristics.Where(c => c.ProductVariantId == item.Id).ToListAsync();
                                if (!ProductVariantCharacteristic.IsNullOrEmpty())
                                {
                                    foreach (var ch in ProductVariantCharacteristic)
                                    {
                                        _context.ProductVariantCharacteristics.Remove(ch);

                                    }
                                    await _context.SaveChangesAsync();
                                }
                                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.ProductVariantId == item.Id);
                                _context.Stocks.Remove(stock);
                                _context.ProductVariants.Remove(item);

                            }
                            await _context.SaveChangesAsync();
                        }
                        _context.Products.Remove(pr);

                    }
                    await _context.SaveChangesAsync();

                }

                Company = company;
                _context.Companies.Remove(Company);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
