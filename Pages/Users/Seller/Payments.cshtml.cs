using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Pages.Users.Seller
{
    [Authorize(Roles = "Seller,Admin")]
    public class PaymentsModel : PageModel
    {
        private readonly AppDbContext _context;

        public PaymentsModel(AppDbContext context)
        {
            _context = context;
        }

        public Company? Company { get; set; }
        public IList<SellerPayment> Payments { get; set; } = new List<SellerPayment>();
        public decimal PendingTotal { get; set; }
        public decimal PaidTotal { get; set; }
        public decimal LifetimeTotal { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = await _context.Users
                .Where(u => u.UserName == User.Identity!.Name)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToPage("/Account/Login");
            }

            Company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SellerId == userId && !c.IsDeleted);

            if (Company == null)
            {
                return RedirectToPage("./NoCompany");
            }

            Payments = await _context.SellerPayments
                .AsNoTracking()
                .Include(p => p.Currency)
                .Include(p => p.Order)
                .Where(p => p.CompanyId == Company.Id && !p.IsDeleted)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            PendingTotal = Payments
                .Where(p => !p.IsPaidToSeller && p.Status != PaymentStatus.Failed)
                .Sum(GetSellerNetAmount);

            PaidTotal = Payments
                .Where(p => p.IsPaidToSeller || p.Status == PaymentStatus.Completed)
                .Sum(GetSellerNetAmount);

            LifetimeTotal = Payments
                .Where(p => p.Status != PaymentStatus.Failed)
                .Sum(GetSellerNetAmount);

            return Page();
        }

        public decimal GetGrossAmount(SellerPayment payment)
        {
            return payment.GrossAmount ?? payment.ProductTotal + payment.DeliveryFee;
        }

        public decimal GetPlatformCommission(SellerPayment payment)
        {
            if (payment.PlatformCommission.HasValue)
            {
                return payment.PlatformCommission.Value;
            }

            return payment.ProductTotal * payment.SalesCommission;
        }

        public decimal GetSellerNetAmount(SellerPayment payment)
        {
            if (payment.SellerAmount.HasValue)
            {
                return payment.SellerAmount.Value;
            }

            return Math.Max(0, GetGrossAmount(payment) - GetPlatformCommission(payment));
        }

        public string FormatMoney(decimal amount, string? currencyCode)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:N0} {1}", amount, string.IsNullOrWhiteSpace(currencyCode) ? "HTG" : currencyCode);
        }
    }
}
