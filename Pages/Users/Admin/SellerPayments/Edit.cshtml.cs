using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;

namespace MCBusiness.Pages_Users_Admin_SellerPayments
{
    public class EditModel : PageModel
    {
        private readonly MCBusiness.Models.AppDbContext _context;
        private readonly IAmazonLikeDistributionService _distributionService;
        private readonly IPlatformTransactionAuditService _auditService;
        private readonly IWalletService _walletService;

        public EditModel(
            MCBusiness.Models.AppDbContext context,
            IAmazonLikeDistributionService distributionService,
            IPlatformTransactionAuditService auditService,
            IWalletService walletService)
        {
            _context = context;
            _distributionService = distributionService;
            _auditService = auditService;
            _walletService = walletService;
        }

        [BindProperty]
        public SellerPayment SellerPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sellerPayment =  await _context.SellerPayments.FirstOrDefaultAsync(m => m.Id == id);
            if (sellerPayment == null)
            {
                return NotFound();
            }

            SellerPayment = sellerPayment;
            LoadSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                LoadSelectLists();
                return Page();
            }

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var existing = await _context.SellerPayments
                    .FirstOrDefaultAsync(sp => sp.Id == SellerPayment.Id);

                if (existing == null)
                {
                    return NotFound();
                }

                if (existing.IsPaidToSeller && !SellerPayment.IsPaidToSeller)
                {
                    ModelState.AddModelError(string.Empty, "Un paiement vendeur confirme ne peut pas etre annule depuis cette page.");
                    SellerPayment = existing;
                    LoadSelectLists();
                    return Page();
                }

                if (!existing.IsPaidToSeller && SellerPayment.IsPaidToSeller)
                {
                    var distribution = await _distributionService.CalculateDistributionAsync(existing.OrderId, existing.CompanyId);
                    if (!distribution.Success)
                    {
                        ModelState.AddModelError(string.Empty, distribution.ErrorMessage ?? "Calcul du paiement vendeur impossible.");
                        SellerPayment = existing;
                        LoadSelectLists();
                        return Page();
                    }

                    var promoAlreadyRecorded = await _context.PlatformTransactions.AnyAsync(pt =>
                        pt.OrderId == existing.OrderId &&
                        pt.Category == PlatformTransactionCategory.PromoDiscount &&
                        !pt.IsDeleted);

                    await _auditService.RecordDistributionAsync(
                        distribution,
                        existing.OrderId,
                        existing.CompanyId,
                        existing.SellerAmount ?? existing.SubTotal,
                        !promoAlreadyRecorded);

                    var sellerId = await _context.Companies
                        .Where(c => c.Id == existing.CompanyId)
                        .Select(c => c.SellerId)
                        .FirstOrDefaultAsync();
                    var sellerAmount = existing.SellerAmount ?? existing.SubTotal;
                    var settlementTransactionId = $"seller_payment_settlement_{existing.Id}";
                    var alreadySettled = await _context.WalletTransactions.AnyAsync(wt =>
                        wt.TransactionId == settlementTransactionId &&
                        wt.WalletType == WalletType.Earnings &&
                        wt.Type == WalletTransactionType.Withdrawal &&
                        !wt.IsDeleted);

                    if (!string.IsNullOrWhiteSpace(sellerId) && sellerAmount > 0m && !alreadySettled)
                    {
                        await _walletService.DebitAsync(
                            sellerId,
                            WalletType.Earnings,
                            sellerAmount,
                            WalletTransactionType.Withdrawal,
                            settlementTransactionId,
                            $"Versement vendeur paiement #{existing.Id}",
                            existing.CurrencyBase);
                    }

                    existing.IsPaidToSeller = true;
                    existing.PaidToSellerAt = SellerPayment.PaidToSellerAt ?? DateTimeOffset.UtcNow;
                    existing.Status = PaymentStatus.Completed;
                    existing.PaymentMethod = SellerPayment.PaymentMethod;
                    existing.ExternalTransactionId = SellerPayment.ExternalTransactionId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SellerPaymentExists(SellerPayment.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                LoadSelectLists();
                return Page();
            }

            return RedirectToPage("./Index");
        }

        private bool SellerPaymentExists(int id)
        {
            return _context.SellerPayments.Any(e => e.Id == id);
        }

        private void LoadSelectLists()
        {
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            ViewData["CurrencyBase"] = new SelectList(_context.Currencies, "Id", "CurrencyBase");
            ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id");
        }
    }
}
