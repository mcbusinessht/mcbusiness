using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace MCBusiness.Pages.Users.Admin.BankTransferPayments
{
    [MCBusiness.Attributes.IdempotentAction(timeoutSeconds: 10, keyPrefix: "ApproveBankTransfer")]
    public class ApproveModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ApproveModel> _logger;
        private readonly IWebHostEnvironment _env;

        private readonly MCBusiness.Services.INotificationService _notificationService;
        private readonly MCBusiness.Services.IPlatformTransactionAuditService _auditService;
        private readonly MCBusiness.Services.IWalletService _walletService;

        public ApproveModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ApproveModel> logger,
            IWebHostEnvironment env,
            MCBusiness.Services.INotificationService notificationService,
            MCBusiness.Services.IPlatformTransactionAuditService auditService,
            MCBusiness.Services.IWalletService walletService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _env = env;
            _notificationService = notificationService;
            _auditService = auditService;
            _walletService = walletService;
        }

        [BindProperty] public string Password { get; set; }
        [BindProperty] public IFormFile ProofFilePath { get; set; }
        [BindProperty] public string AdminComment { get; set; }

        public BankTransferPayment Transfer { get; set; }
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Transfer = await _context.BankTransferPayments
                .Include(t => t.User)
                .ThenInclude(u => u.Wallets)
                .Include(t => t.Currency)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (Transfer == null)
            {
                ErrorMessage = "Virement introuvable";
                return Page();
            }

            return Page();
        }
        public async Task<IActionResult> OnPostAsync(int id)
        {
            try
            {
                var admin = await _userManager.GetUserAsync(User);
                if (!await _userManager.CheckPasswordAsync(admin, Password))
                {
                    ErrorMessage = "Mot de passe incorrect";
                    return await OnGetAsync(id);
                }

                // IMPORTANT: Avec NpgsqlRetryingExecutionStrategy, toute transaction utilisateur doit
                // être exécutée via CreateExecutionStrategy().ExecuteAsync pour permettre le retry.
                var strategy = _context.Database.CreateExecutionStrategy();
                IActionResult? execResult = null;
                await strategy.ExecuteAsync(async () =>
                {
                    await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
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
                            ErrorMessage = "Ce virement a déjà été traité.";
                            execResult = RedirectToPage("./Index");
                            return;
                        }

                        // Sauvegarde de la preuve (si uploadée)
                        if (Transfer.Direction == BankTransferDirection.Withdrawal && ProofFilePath != null)
                        {
                            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "proofs");
                            Directory.CreateDirectory(uploadDir);

                            var extension = Path.GetExtension(ProofFilePath.FileName);
                            var fileName = $"proof_{Transfer.Id}_{DateTimeOffset.UtcNow.Ticks}{extension}";
                            var fullPath = Path.Combine(uploadDir, fileName);

                            using (var stream = new FileStream(fullPath, FileMode.Create))
                            {
                                await ProofFilePath.CopyToAsync(stream);
                            }

                            Transfer.ProofFilePath = $"/uploads/proofs/{fileName}";
                        }

                        // Ajouter le commentaire
                        if (!string.IsNullOrWhiteSpace(AdminComment))
                        {
                            Transfer.AdminComment = AdminComment;
                        }

                        // Validation
                        Transfer.Status = BankTransferStatus.Approved;
                        Transfer.ReviewedAt = DateTimeOffset.UtcNow;
                        Transfer.ReviewedBy = admin.Id;

                        if (Transfer.Direction == BankTransferDirection.Deposit)
                        {
                            var userWallet = await _walletService.GetOrCreateWalletAsync(Transfer.UserId, WalletType.Customer, Transfer.CurrencyId);

                            userWallet.Balance += Transfer.Amount;
                            userWallet.LastUpdated = DateTimeOffset.UtcNow;

                            _context.WalletTransactions.Add(new WalletTransaction
                            {
                                WalletId = userWallet.Id,
                                UserId = Transfer.UserId,
                                Amount = Transfer.Amount,
                                Balance = userWallet.Balance,
                                CurrencyBase = userWallet.CurrencyBase,
                                WalletType = WalletType.Customer,
                                Type = WalletTransactionType.Deposit,
                                TransactionId = Transfer.TransactionId.ToString(),
                                Description = $"Dépôt bancaire approuvé #{Transfer.Id}"
                            });
                        }

                        var existingTransaction = await _context.Transactions
                            .FirstOrDefaultAsync(t => t.Id == Transfer.TransactionId && t.UserId == Transfer.UserId);

                        if (existingTransaction == null)
                        {
                            ErrorMessage = "Transaction associée introuvable";
                            execResult = await OnGetAsync(id);
                        }
                        else
                        {
                            existingTransaction.Status = TransactionStatus.Completed;
                            _context.Transactions.Update(existingTransaction);

                            // Audit plateforme : dépôt ou retrait utilisateur
                            var extId = $"banktransfer_{Transfer.Id}";
                            if (Transfer.Direction == BankTransferDirection.Deposit)
                                await _auditService.RecordUserDepositAsync(Transfer.UserId, Transfer.Amount, "BankTransfer", extId, $"Dépôt virement approuvé #{Transfer.Id}");
                            else
                                await _auditService.RecordUserWithdrawalAsync(Transfer.UserId, Transfer.Amount, 0, "BankTransfer", extId, $"Retrait virement approuvé #{Transfer.Id}");

                            // Notification utilisateur (plateforme + email)
                            await _notificationService.NotifyAsync(new Notification
                            {
                                UserId = Transfer.UserId,
                                Title = "Virement approuvé",
                                Message = $"Votre virement de {Transfer.Amount:C} a été approuvé.",
                                IsRead = false,
                                CreatedAt = DateTimeOffset.UtcNow
                            });

                            await _context.SaveChangesAsync();
                            await dbTransaction.CommitAsync();

                            execResult = RedirectToPage("./Index");
                        }
                    }
                    catch
                    {
                        await dbTransaction.RollbackAsync();
                        throw;
                    }
                });

                return execResult ?? await OnGetAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'approbation du virement.");
                ErrorMessage = "Une erreur est survenue lors du traitement.";
                return await OnGetAsync(id);
            }
        }

        
    }
}


