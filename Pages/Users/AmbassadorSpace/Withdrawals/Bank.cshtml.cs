using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using MCBusiness.Services;

namespace MCBusiness.Pages.Users.AmbassadorSpace.Withdrawals
{
    public class BankModel : PageModel  
    {
        private readonly ILogger<BankModel> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWalletService _walletService;

        public BankModel(
            ILogger<BankModel> logger,
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWalletService walletService)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _walletService = walletService;
        }

        [BindProperty]
        public BankTransferInputModel Input { get; set; }

        public decimal CurrentBalance { get; set; }
        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }

        public class BankTransferInputModel
        {
            [Required(ErrorMessage = "Le montant est requis")]
            [Range(100, double.MaxValue, ErrorMessage = "Le montant minimum est de 100 HTG")]
            public decimal Amount { get; set; }

            [Required(ErrorMessage = "Le nom de la banque est requis")]
            public string BankName { get; set; }

            [Required(ErrorMessage = "Le nom du titulaire est requis")]
            public string AccountName { get; set; }

            [Required(ErrorMessage = "Le numéro de compte est requis")]
            public string AccountNumber { get; set; }

            [Required(ErrorMessage = "Le type de virement est requis")]
            public BankTransferMethod TransferMethod { get; set; }

            [Required(ErrorMessage = "Le mot de passe est requis")]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            CurrentBalance = await GetUserBalance(user.Id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            CurrentBalance = await GetUserBalance(user.Id);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Vérification du mot de passe
            var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, false);
            if (!passwordCheck.Succeeded)
            {
                ErrorMessage = "Mot de passe incorrect";
                return Page();
            }

            // Vérification du solde
            if (Input.Amount > CurrentBalance)
            {
                ErrorMessage = "Solde insuffisant pour effectuer ce retrait";
                return Page();
            }

            // IMPORTANT: exécuter la transaction utilisateur dans l'execution strategy pour supporter les retries Npgsql
            var strategy = _context.Database.CreateExecutionStrategy();
            IActionResult? execResult = null;
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var walletuser = await _walletService.GetOrCreateWalletAsync(user.Id, WalletType.Earnings);
                    if (walletuser == null || walletuser.Balance < Input.Amount)
                    {
                        ErrorMessage = "Solde insuffisant pour effectuer ce retrait";
                        execResult = Page();
                        return;
                    }

                    walletuser.Balance -= Input.Amount;
                    _context.Wallets.Update(walletuser);
                    // Création de la transaction
                    var newTransaction = new Transaction
                    {
                        UserId = user.Id,
                        WalletId = walletuser.Id,
                        WalletType = WalletType.Earnings,
                        Amount = Input.Amount,
                        Type = TransactionType.Withdrawal,
                        Method = TransactionMethod.BankTransfer,
                        Description = $"Retrait bancaire vers {Input.BankName} ({Input.AccountNumber})",
                        Status = TransactionStatus.Pending,
                        CurrencyBase = await GetCurrencyId(CurrencyCode.HTG),
                        OrderId = GenerateOrderId(),
                        // UpdatedAt géré par BaseEntity
                    };

                    _context.Transactions.Add(newTransaction);
                    await _context.SaveChangesAsync(); // Sauvegarde pour obtenir l'ID

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = walletuser.Id,
                        UserId = user.Id,
                        Amount = -Input.Amount,
                        Balance = walletuser.Balance,
                        WalletType = WalletType.Earnings,
                        Type = WalletTransactionType.Withdrawal,
                        TransactionId = newTransaction.Id.ToString(),
                        CurrencyBase = walletuser.CurrencyBase,
                        Description = "Montant reserve pour retrait bancaire"
                    });

                    // Création de la demande de retrait bancaire
                    var withdrawal = new BankTransferPayment
                    {
                        UserId = user.Id,
                        BankName = Input.BankName,
                        AccountName = Input.AccountName,
                        AccountNumber = Input.AccountNumber,
                        TransferMethod = Input.TransferMethod,
                        Amount = Input.Amount,
                        Direction = BankTransferDirection.Withdrawal,
                        Status = BankTransferStatus.Pending,
                        TransactionId = newTransaction.Id,
                        WalletId = walletuser.Id,
                        WalletType = WalletType.Earnings,
                        SubmittedAt = DateTimeOffset.UtcNow
                    };

                    _context.BankTransferPayments.Add(withdrawal);
                    await _context.SaveChangesAsync();

                    await dbTransaction.CommitAsync();

                    SuccessMessage = "Votre demande de retrait a été soumise avec succès. Elle sera traitée sous 24-48 heures.";
                    execResult = Redirect("/Users/User/Transaction");
                }
                catch (Exception ex)   
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Erreur lors de la demande de retrait bancaire");
                    ErrorMessage = "Une erreur est survenue lors du traitement de votre demande. Veuillez réessayer.";
                    execResult = Page();
                }
            });
            return execResult ?? Page();
        }

        private async Task<decimal> GetUserBalance(string userId)
        {
            try
            {
                // Option 1: Si vous avez un Wallet séparé
                var wallet = await _walletService.GetWalletAsync(userId, WalletType.Earnings);
                
                if (wallet != null)
                {
                    return wallet.Balance;
                }

                // Option 2: Calcul à partir des transactions (fallback)
                var completedDeposits = await _context.Transactions
                    .Where(t => t.UserId == userId && 
                               t.Type == TransactionType.Deposit && 
                               t.Status == TransactionStatus.Completed)
                    .SumAsync(t => t.Amount);

                var completedWithdrawals = await _context.Transactions
                    .Where(t => t.UserId == userId && 
                               t.Type == TransactionType.Withdrawal && 
                               t.Status == TransactionStatus.Completed)
                    .SumAsync(t => t.Amount);

                return completedDeposits - completedWithdrawals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du calcul du solde utilisateur");
                return 0;
            }
        }

        private async Task<int> GetCurrencyId(CurrencyCode currencyCode)
        {
            var currencyCodeValue = currencyCode.ToString();
            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Code == currencyCodeValue);
            
            return currency?.Id ?? 1; // Retourne 1 comme valeur par défaut si non trouvé
        }

        private string GenerateOrderId()
        {
            string orderId;
            do
            {
                orderId = new Random().NextInt64(100000000000, 999999999999).ToString();
            } while (_context.Transactions.Any(t => t.OrderId == orderId));
            
            return orderId;
        }
    }
}
