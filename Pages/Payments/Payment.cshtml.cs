using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using MCBusiness.Interfaces;
using MCBusiness.Services;

namespace MCBusiness.Pages.Payments
{
    [MCBusiness.Attributes.IdempotentAction(timeoutSeconds: 15, keyPrefix: "WalletPayment")]
    public class PaymentModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<PaymentModel> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly INotificationService _notificationService;
        private readonly IPlatformTransactionAuditService _auditService;
        private readonly IStockReservationService _stockReservationService;
        private readonly IWalletService _walletService;

        [BindProperty(SupportsGet = true)] public string Type { get; set; }
        [BindProperty(SupportsGet = true)] public int? Id { get; set; }
        
        [BindProperty] public string PaymentType { get; set; }
        [BindProperty] public string PromoCodeInput { get; set; }
        [BindProperty] public int? PlanId { get; set; }
        [BindProperty] public int? OrderId { get; set; }
        [BindProperty] public int WalletId { get; set; }

        public AmbassadorPlan Plan { get; set; }
        public Order Order { get; set; }
        public Wallet Wallet { get; set; }
        public ApplicationUser CurrentUser { get; set; }
        public PromoCode PromoCode { get; set; }
        public PromoCodeUsage PromoCodeUsage { get; set; }

        public decimal BaseAmount => PaymentType == "plan"
            ? Plan?.Price ?? 0
            : Order?.GrandTotal ?? 0;

        public decimal DiscountBasis => PaymentType == "order"
            ? Order?.TotalAmount ?? 0
            : BaseAmount;

        public decimal ShippingAmount => PaymentType == "order"
            ? Order?.ShippingTotal ?? 0
            : 0;

        public bool HasEnoughBalance => Wallet != null && Wallet.Balance >= BaseAmount;

        public PaymentModel(AppDbContext context, IEmailSender emailSender, ILogger<PaymentModel> logger, 
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, INotificationService notificationService, 
            IPlatformTransactionAuditService auditService, IStockReservationService stockReservationService, IWalletService walletService)
        {
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
            _notificationService = notificationService;
            _auditService = auditService;
            _stockReservationService = stockReservationService;
            _walletService = walletService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadBasicData();
            if (PaymentType == "plan") await LoadPlanData();
            else if (PaymentType == "order") await LoadOrderData();
            else return Redirect("/Index");

            return Page();
        }

        public async Task<JsonResult> OnGetValidatePromoAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new JsonResult(new { valid = false, message = "Code requis" });

            await LoadBasicData();
            if (PaymentType == "plan") await LoadPlanData();
            else if (PaymentType == "order") await LoadOrderData();
            else return new JsonResult(new { valid = false, message = "Type de paiement invalide" });

            var promo = await _context.PromoCodes
                .Include(p => p.PromoCampaign)
                .FirstOrDefaultAsync(p => p.Code.ToLower() == code.ToLower() && p.IsActive);

            if (promo == null || !promo.PromoCampaign.IsActive || promo.PromoCampaign.EndDate < DateTimeOffset.UtcNow)
                return new JsonResult(new { valid = false, message = "Code invalide ou expiré" });

            if (PaymentType == "order" && !promo.IsOrderAvailable)
                return new JsonResult(new { valid = false, message = "Ce code n'est pas valide pour les commandes" });

            if (PaymentType == "plan" && !promo.IsPlanAvailable)
                return new JsonResult(new { valid = false, message = "Ce code n'est pas valide pour les plans" });

            if (promo.CreatedByUserId == CurrentUser.Id)
                return new JsonResult(new { valid = false, message = "Vous ne pouvez pas utiliser votre propre code promo" });

            var campaignId = promo.PromoCampaignId;
            var alreadyUsed = await _context.PromoCodeUsages
                .Include(u => u.PromoCode)
                .AnyAsync(u => u.UsedByUserId == CurrentUser.Id && u.PromoCode.PromoCampaignId == campaignId);

            if (alreadyUsed)
                return new JsonResult(new { valid = false, message = "Vous avez deja utilise une promotion de cette campagne" });

            var discount = promo.DiscountPercentage > 0
                ? DiscountBasis * (promo.DiscountPercentage / 100m)
                : promo.RewardAmount;
            discount = Math.Min(discount, DiscountBasis);

            return new JsonResult(new {
                valid = true,
                message = "Code valide",
                discountAmount = Math.Round(discount, 2),
                discountPercent = promo.DiscountPercentage,
                rewardAmount = promo.RewardAmount
            });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Redirect("/Index");

            try
            {
                await LoadBasicData();
                await ValidatePromoCodeAsync();

                var saveChangesAfterProcessing = true;

                if (PaymentType == "plan" && PlanId.HasValue)
                {
                    await LoadPlanData();
                    await ValidatePlanPayment();
                    await ProcessPlanPaymentInTransaction();
                    saveChangesAfterProcessing = false;
                }
                else if (PaymentType == "order" && OrderId.HasValue)
                {
                    await LoadOrderData();
                    if (!await ValidateOrderPaymentWithModelState())
                    {
                        await ReloadPageData();
                        return Page();
                    }
                    await ProcessOrderPaymentInTransaction();
                    saveChangesAfterProcessing = false;
                }
                else throw new Exception("Type de paiement invalide");

                if (saveChangesAfterProcessing)
                {
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Paiement effectue avec succes !";
                if (PaymentType == "order" && OrderId.HasValue)
                {
                    return RedirectToPage("/Orders/Receipt", new { id = OrderId.Value });
                }

                return RedirectToPage("/Users/User/Transaction");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur de paiement");
                ModelState.AddModelError("", ex.Message);
                await ReloadPageData();
                return Page();
            }
        }

        private async Task LoadBasicData()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null) throw new Exception("Utilisateur non connecté");

            Wallet = await _walletService.GetOrCreateWalletAsync(CurrentUser.Id, WalletType.Customer);
            if (Wallet == null) throw new Exception("Portefeuille par défaut introuvable");

            PaymentType ??= Type;
            PlanId ??= Id;
            OrderId ??= Id;
        }

        private async Task LoadPlanData()
        {
            Plan = await _context.AmbassadorPlans
                .FirstOrDefaultAsync(p => p.Id == PlanId && p.IsActive);
            if (Plan == null) throw new Exception("Plan introuvable");
        }

        private async Task LoadOrderData()
        {
            Order = await _context.Orders
                .Include(o => o.Address)
                    .ThenInclude(a => a.City)
                .Include(o => o.Address)
                    .ThenInclude(a => a.State)
                .Include(o => o.Address)
                    .ThenInclude(a => a.Country)
                .Include(o => o.Products)
                    .ThenInclude(op => op.ProductVariant)
                        .ThenInclude(v => v.Product)
                .Include(o => o.Products)
                    .ThenInclude(op => op.Company)
                .FirstOrDefaultAsync(o => o.Id == OrderId && o.UserId == CurrentUser.Id);
            if (Order == null) throw new Exception("Commande introuvable");
        }

        private async Task ValidatePromoCodeAsync()
        {
            if (string.IsNullOrWhiteSpace(PromoCodeInput)) return;

            PromoCode = await _context.PromoCodes
                .Include(p => p.PromoCampaign)
                .FirstOrDefaultAsync(p => p.Code.ToLower() == PromoCodeInput.ToLower() && p.IsActive);

            if (PromoCode == null)
                throw new Exception("Code promo invalide");

            if (!PromoCode.PromoCampaign.IsActive || PromoCode.PromoCampaign.EndDate < DateTimeOffset.UtcNow)
                throw new Exception("Code promo expiré");

            if (PromoCode.CreatedByUserId == CurrentUser.Id)
                throw new Exception("Vous ne pouvez pas utiliser votre propre code promo");

            var campaignId = PromoCode.PromoCampaignId;
            bool alreadyUsed = _context.PromoCodeUsages
                .Include(u => u.PromoCode)
                .Any(u => u.UsedByUserId == CurrentUser.Id && u.PromoCode.PromoCampaignId == campaignId);

            if (alreadyUsed)
                throw new Exception("Vous avez déjà utilisé une promotion de cette campagne.");
        }

        private async Task ValidatePlanPayment()
        {
            if (!Plan.IsActive)
                throw new Exception("Plan non disponible");
            
            var totalAmount = Plan.Price - CalculateDiscount(Plan.Price);
            if (Plan.Price > 0 && Wallet.Balance < totalAmount)
                throw new Exception("Solde insuffisant");

            var subsCount = await _context.AmbassadorSubscriptions
                .Where(s => s.UserId == CurrentUser.Id && s.Status == SubscriptionStatus.Active)
                .CountAsync();
            if (subsCount > 0)
                throw new Exception("Vous avez déjà un abonnement actif, veuillez l'annuler ou attendre son expiration.");
        }

        private async Task<bool> ValidateOrderPaymentWithModelState()
        {
            if (Order.Status != OrderStatus.Pending)
            {
                ModelState.AddModelError("", $"Statut invalide: {Order.Status}");
                return false;
            }
            var totalAmount = Order.GrandTotal - CalculateDiscount(Order.TotalAmount);
            if (Wallet.Balance < totalAmount)
            {
                ModelState.AddModelError("", "Solde insuffisant");
                return false;
            }
            return true;
        }

        private decimal CalculateDiscount(decimal totalAmount)
        {
            decimal discount = 0;
            if (PromoCode != null)
            {
                discount = PromoCode.DiscountPercentage > 0
                    ? totalAmount * (PromoCode.DiscountPercentage / 100m)
                    : PromoCode.RewardAmount;
                discount = Math.Min(discount, totalAmount);
            }
            return discount;
        }

        private async Task ProcessPlanPayment()
        {
            decimal discount = 0;

            if (PromoCodeInput != null && PromoCode != null)
            {
                if (PromoCode.IsPlanAvailable)
                {
                    discount = CalculateDiscount(Plan.Price);
                }
                else
                {
                    throw new Exception("Le code promo n'est pas valide pour l'achat de plan.");
                }
            }

            decimal finalAmount = Plan.Price - discount;
            Wallet.Balance -= finalAmount;
            _context.Wallets.Update(Wallet);

            var subscription = new AmbassadorSubscription
            {
                UserId = CurrentUser.Id,
                PlanId = Plan.Id,
                StartDate = DateTimeOffset.UtcNow,
                Status = SubscriptionStatus.Active
            };
            await _context.AmbassadorSubscriptions.AddAsync(subscription);
            await _context.SaveChangesAsync();

            var payment = new Payment
            {
                UserId = CurrentUser.Id,
                Amount = finalAmount,
                PaymentMethod = PaymentMethod.Wallet,
                Status = PaymentStatus.Completed,
                AmbassadorSubscriptionId = subscription.Id,
                TransactionId = await GenerateUniqueOrderIdAsync()
            };
            await _context.Payments.AddAsync(payment);

            // AUDIT : Revenu abonnement (RecordSubscriptionPaymentAsync, pas OrderRevenue)
            try
            {
                await _auditService.RecordSubscriptionPaymentAsync(
                    CurrentUser.Id,
                    finalAmount,
                    subscription.Id,
                    $"Paiement plan ambassadeur {Plan.Name} - Wallet"
                );
                _logger.LogInformation("Audit PlatformTransaction plan enregistré.");
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Erreur audit plan obligatoire : {Message}", auditEx.Message);
                throw new InvalidOperationException("Le paiement du plan ne peut pas etre comptabilise.", auditEx);
            }

            var transaction = new Transaction
            {
                UserId = CurrentUser.Id,
                WalletId = Wallet.Id,
                WalletType = WalletType.Customer,
                Amount = finalAmount,
                CurrencyBase = Wallet.CurrencyBase,
                Type = TransactionType.Payment,
                Status = TransactionStatus.Completed,
                Method = TransactionMethod.MCBusinessPay,
                TransactionId = payment.TransactionId,
                OrderId = payment.TransactionId,
                Description = $"Paiement plan ambassadeur {Plan.Name}",
                CompletedAt = DateTimeOffset.UtcNow
            };
            await _context.Transactions.AddAsync(transaction);

            var walletTransaction = new WalletTransaction
            {
                WalletId = Wallet.Id,
                UserId = CurrentUser.Id,
                WalletType = WalletType.Customer,
                Amount = -finalAmount,
                Balance = Wallet.Balance,
                CurrencyBase = Wallet.CurrencyBase,
                Type = WalletTransactionType.Payment,
                Description = $"Paiement plan {Plan.Name}",
                TransactionId = payment.TransactionId
            };
            await _context.WalletTransactions.AddAsync(walletTransaction);

            if (PromoCode != null)
            {
                PromoCodeUsage = new PromoCodeUsage
                {
                    PromoCodeId = PromoCode.Id,
                    UsedByUserId = CurrentUser.Id,
                    DiscountAmount = discount,
                    UsedAt = DateTimeOffset.UtcNow
                };
                await _context.PromoCodeUsages.AddAsync(PromoCodeUsage);

                // AUDIT CRITIQUE : Enregistrer le coût de la promotion plan
                try
                {
                    await _auditService.RecordPromoDiscountAsync(
                        subscription.Id,
                        discount,
                        PromoCode.Code,
                        $"Promotion plan {Plan.Name} - Code {PromoCode.Code}"
                    );
                    _logger.LogInformation("Audit PlatformTransaction promo (plan) enregistré.");
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Erreur audit promo plan obligatoire : {Message}", auditEx.Message);
                    throw new InvalidOperationException("La promotion du plan ne peut pas etre comptabilisee.", auditEx);
                }

                try
                {
                    var newNotif = new Notification
                    {
                        UserId = PromoCode.CreatedByUserId,
                        Title = "Utilisation de votre code Promo",
                        Message = $"Vous venez de recevoir un nouveau bonus via votre code promotionnel de {PromoCode.RewardAmount} HTG",
                    };
                    await _notificationService.NotifyAsync(newNotif);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{e}, Erreur lors de l'envoi de la notification par email.");
                }
            }

            var notification = new Notification
            {
                UserId = CurrentUser.Id,
                Title = "Paiement effectué avec succès",
                Message = $"Félicitations ! Vous avez activé le plan {Plan.Name}. Boostez vos gains dès maintenant en partageant votre lien et code de parrainage depuis votre tableau de bord. Plus vous invitez, plus vous gagnez !",
                CreatedAt = DateTimeOffset.UtcNow,
                IsRead = false
            };
            await _notificationService.NotifyAsync(notification);

            var unreadNotifCount = await _context.Notifications
                .Where(n => n.UserId == CurrentUser.Id && !n.IsRead)
                .CountAsync();
            unreadNotifCount++;

            string html_notification = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails", "new_notification.html"));
            html_notification = html_notification.Replace("{{FullName}}", CurrentUser.UserName);
            html_notification = html_notification.Replace("{{unreadNotifCount}}", unreadNotifCount.ToString());

            try
            {
                await _emailSender.SendEmailAsync(CurrentUser.Email, "Nouvelle notification", html_notification);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e}, Erreur lors de l'envoi de la notification par email.");
            }

            // Assigner le rôle "Ambassador" à l'utilisateur
            if (!await _userManager.IsInRoleAsync(CurrentUser, "Ambassador"))
            {
                await _userManager.AddToRoleAsync(CurrentUser, "Ambassador");
            }
        }

        private async Task ProcessOrderPayment()
        {
            if (Order.Status == OrderStatus.Paid)
            {
                _logger.LogWarning("ProcessOrderPayment: commande {OrderId} déjà payée, abandon", Order.Id);
                throw new InvalidOperationException("Cette commande est déjà payée.");
            }

            decimal discount = 0;

            if (PromoCodeInput != null && PromoCode != null)
            {
                if (PromoCode.IsOrderAvailable)
                {
                    discount = CalculateDiscount(Order.TotalAmount);
                }
                else
                {
                    throw new Exception("Le code promo n'est pas valide pour l'achat de produits.");
                }
            }

            decimal finalAmount = Order.GrandTotal - discount;

            // Réduire le stock à la confirmation du paiement (idempotent si déjà Payée)
            await _stockReservationService.ReduceStockForOrderAsync(Order.Id);

            Wallet.Balance -= finalAmount;
            _context.Wallets.Update(Wallet);

            Order.Status = OrderStatus.Paid;
            _context.Orders.Update(Order);

            var payment = new Payment
            {
                UserId = CurrentUser.Id,
                Amount = finalAmount,
                PaymentMethod = PaymentMethod.Wallet,
                Status = PaymentStatus.Completed,
                OrderId = Order.Id,
                TransactionId = await GenerateUniqueOrderIdAsync()
            };
            await _context.Payments.AddAsync(payment);

            // AUDIT CRITIQUE : Enregistrer revenu commande
            try
            {
                await _auditService.RecordOrderRevenueAsync(
                    Order.Id,
                    finalAmount,
                    $"Paiement commande n°{Order.Id} - Wallet"
                );
                _logger.LogInformation("Audit PlatformTransaction commande enregistré.");
            }
            catch (Exception auditEx)
            {
                _logger.LogError($"Erreur audit commande obligatoire : {auditEx.Message}");
                throw new InvalidOperationException("Le paiement de la commande ne peut pas etre comptabilise.", auditEx);
            }

            var transaction = new Transaction
            {
                UserId = CurrentUser.Id,
                WalletId = Wallet.Id,
                WalletType = WalletType.Customer,
                Amount = finalAmount,
                Type = TransactionType.Payment,
                Status = TransactionStatus.Completed,
                Method = TransactionMethod.MCBusinessPay,
                CurrencyBase = Wallet.CurrencyBase,
                CompletedAt = DateTimeOffset.UtcNow,
                TransactionId = payment.TransactionId,
                OrderId = payment.TransactionId,
                Description = $"Paiement commande n°{Order.Id}"
            };
            await _context.Transactions.AddAsync(transaction);

            var walletTransaction = new WalletTransaction
            {
                WalletId = Wallet.Id,
                Amount = -finalAmount,
                WalletType = WalletType.Customer,
                Type = WalletTransactionType.Payment,
                UserId = CurrentUser.Id,
                Balance = Wallet.Balance,
                CurrencyBase = Wallet.CurrencyBase,
                Description = $"Paiement commande n°{Order.Id}",
                TransactionId = payment.TransactionId
            };
            await _context.WalletTransactions.AddAsync(walletTransaction);

            if (PromoCode != null)
            {
                PromoCodeUsage = new PromoCodeUsage
                {
                    PromoCodeId = PromoCode.Id,
                    UsedByUserId = CurrentUser.Id,
                    DiscountAmount = discount,
                    UsedAt = DateTimeOffset.UtcNow
                };
                await _context.PromoCodeUsages.AddAsync(PromoCodeUsage);

                // AUDIT CRITIQUE : Enregistrer le coût de la promotion commande
                try
                {
                    await _auditService.RecordPromoDiscountAsync(
                        Order.Id,
                        discount,
                        PromoCode.Code,
                        $"Promotion commande n°{Order.Id} - Code {PromoCode.Code}"
                    );
                    _logger.LogInformation("Audit PlatformTransaction promo (commande) enregistré.");
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Erreur audit promo commande obligatoire : {Message}", auditEx.Message);
                    throw new InvalidOperationException("La promotion de la commande ne peut pas etre comptabilisee.", auditEx);
                }

                try
                {
                    var newNotif = new Notification
                    {
                        UserId = PromoCode.CreatedByUserId,
                        Title = "Utilisation de votre code Promo",
                        Message = $"Vous venez de recevoir un nouveau bonus via votre code promotionnel de {PromoCode.RewardAmount} HTG",
                    };
                    await _notificationService.NotifyAsync(newNotif);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{e}, Erreur lors de l'envoi de la notification par email.");
                }
            }

            var notification = new Notification
            {
                UserId = CurrentUser.Id,
                Title = "Paiement effectué avec succès",
                Message = $"Votre paiement pour la commande n°{Order.Id} a été traité avec succès. Vous pouvez suivre l'évolution de votre commande dans votre espace personnel.",
                CreatedAt = DateTimeOffset.UtcNow,
                IsRead = false
            };

            await _notificationService.NotifyAsync(notification);

            // Notification du vendeur 
            var supplierIds = await _context.OrderProducts
                .Where(op => op.OrderId == Order.Id)
                .Select(op => op.Company.SellerId)
                .Distinct()
                .ToListAsync();

            if (supplierIds != null)
            {
                foreach (var supplierId in supplierIds)
                {
                    var notification4 = new Notification
                    {
                        UserId = supplierId,
                        Title = "Nouvelle commande payée",
                        Message = "Une commande incluant au moins un de vos produits a été payée. Veuillez consulter vos commandes pour plus de détails.",
                    };
                    await _notificationService.NotifyAsync(notification4);
                }
            }
        }

        private async Task ProcessPlanPaymentInTransaction()
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                await LoadBasicData();
                await LoadPlanData();
                await ValidatePlanPayment();
                await ProcessPlanPayment();
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            });
        }

        private async Task ProcessOrderPaymentInTransaction()
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                await LoadBasicData();
                await LoadOrderData();

                if (!await ValidateOrderPaymentWithModelState())
                {
                    throw new InvalidOperationException("Paiement commande invalide.");
                }

                await ProcessOrderPayment();
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            });
        }

        private async Task ReloadPageData()
        {
            await LoadBasicData();
            if (PaymentType == "plan") await LoadPlanData();
            else await LoadOrderData();
        }

        private async Task<string> GenerateUniqueOrderIdAsync()
        {
            string id;
            do
            {
                id = GenerateSecureNumericId();
            } while (await _context.Transactions.AnyAsync(t => t.OrderId == id));
            return id;
        }

        private static string GenerateSecureNumericId()
        {
            var bytes = RandomNumberGenerator.GetBytes(sizeof(ulong));
            var value = BitConverter.ToUInt64(bytes, 0);
            return (100000000000UL + (value % 900000000000UL)).ToString();
        }
    }
}
