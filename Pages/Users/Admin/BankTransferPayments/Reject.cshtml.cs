using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
namespace MCBusiness.Pages.Users.Admin.BankTransferPayments
{
    [MCBusiness.Attributes.IdempotentAction(timeoutSeconds: 10, keyPrefix: "RejectBankTransfer")]
    public class RejectModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RejectModel> _logger;

        private readonly MCBusiness.Services.INotificationService _notificationService;
        private readonly MCBusiness.Services.IWalletService _walletService;
        public RejectModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<RejectModel> logger,
            MCBusiness.Services.INotificationService notificationService,
            MCBusiness.Services.IWalletService walletService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService;
            _walletService = walletService;
        }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string AdminComment { get; set; }

        public BankTransferPayment Transfer { get; set; }
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                Transfer = await _context.BankTransferPayments
                    .Include(t => t.User)
                    .Include(t => t.Currency)
                    .FirstOrDefaultAsync(t => t.Id == id);

                return Transfer == null ? NotFound() : Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur de chargement du virement");
                ErrorMessage = "Erreur lors du chargement des données";
                return Page();
            }
        }
        public async Task<IActionResult> OnPostAsync(int id)
        {
            try
            {
                var adminUser = await _userManager.GetUserAsync(User);
                if (!await _userManager.CheckPasswordAsync(adminUser, Password))
                {
                    ErrorMessage = "Mot de passe incorrect";
                    return await OnGetAsync(id);
                }

                // IMPORTANT: exécuter la transaction utilisateur dans l'execution strategy pour supporter les retries Npgsql
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        Transfer = await _context.BankTransferPayments
                            .Include(t => t.User)
                            .ThenInclude(u => u.Wallets)
                            .Include(t => t.Currency)
                            .FirstAsync(t => t.Id == id);

                        // PROTECTION IDEMPOTENCE: Vérifier que le virement n'a pas déjà été traité
                        if (!MCBusiness.Helpers.IdempotencyHelper.CanProcessBankTransfer(Transfer))
                        {
                            _logger.LogWarning($"Tentative de rejet d'un virement déjà traité (ID: {id})");
                            return RedirectToPage("./Index");
                        }

                        // Mise à jour du statut
                        Transfer.Status = BankTransferStatus.Rejected;
                        Transfer.ReviewedAt = DateTimeOffset.UtcNow;
                        Transfer.ReviewedBy = adminUser.Id;
                        Transfer.AdminComment = AdminComment;

                        // Mise à jour de la transaction associée si existe
                        var transactionToUpdate = await _context.Transactions
                            .FirstOrDefaultAsync(t => t.Id == Transfer.TransactionId);
                        if (transactionToUpdate != null)
                        {
                            if (Transfer.Direction == BankTransferDirection.Withdrawal)
                            {
                                var walletType = transactionToUpdate.WalletType != default
                                    ? transactionToUpdate.WalletType
                                    : Transfer.WalletType;
                                var wallet = await _walletService.GetOrCreateWalletAsync(Transfer.UserId, walletType, Transfer.CurrencyId);

                                wallet.Balance += Transfer.Amount;
                                _context.WalletTransactions.Add(new WalletTransaction
                                {
                                    WalletId = wallet.Id,
                                    UserId = Transfer.UserId,
                                    Amount = Transfer.Amount,
                                    Balance = wallet.Balance,
                                    WalletType = walletType,
                                    Type = WalletTransactionType.Refund,
                                    TransactionId = transactionToUpdate.Id.ToString(),
                                    CurrencyBase = wallet.CurrencyBase,
                                    Description = $"Liberation du retrait bancaire rejete #{Transfer.Id}"
                                });
                            }

                            transactionToUpdate.Status = TransactionStatus.Failed;
                            _context.Update(transactionToUpdate);
                        }

                        // Notification utilisateur (plateforme + email)
                        await _notificationService.NotifyAsync(new Notification
                        {
                            UserId = Transfer.UserId,
                            Title = "Virement rejeté",
                            Message = $"Votre virement de {Transfer.Amount:C} a été rejeté. Raison : {AdminComment}. Si vous souhaitez contester, veuillez contacter la plateforme.",
                            IsRead = false,
                            CreatedAt = DateTimeOffset.UtcNow
                        });

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return RedirectToPage("./Index");
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur de rejet du virement");
                ErrorMessage = "Une erreur est survenue lors du rejet";
                return await OnGetAsync(id);
            }
        }
    }
}

