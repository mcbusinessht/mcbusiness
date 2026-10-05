using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using MCBusiness.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

namespace MCBusiness.Pages.Users.AmbassadorSpace.Withdrawals
{
    [Authorize(Policy = "AmbassadorPolicy")]
    [ValidateAntiForgeryToken]
    public class MonCashModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MonCashService _monCashService;
        private readonly ISecureFinancialService _secureFinancialService;
        private readonly IWalletService _walletService;
        private readonly IReCaptchaService _reCaptchaService;
        private readonly ReCaptchaSettings _reCaptchaSettings;

        public MonCashModel(AppDbContext context, UserManager<ApplicationUser> userManager, 
            MonCashService monCashService, ISecureFinancialService secureFinancialService,
            IReCaptchaService reCaptchaService, IOptions<ReCaptchaSettings> reCaptchaSettings,
            IWalletService walletService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _monCashService = monCashService;
            _secureFinancialService = secureFinancialService;
            _walletService = walletService;
            _reCaptchaService = reCaptchaService;
            _reCaptchaSettings = reCaptchaSettings.Value;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public ApplicationUser CurrentUser { get; set; }
        public Wallet CurrentWallet { get; set; }
        public string StatusMessage { get; set; }
        public string ReCaptchaSiteKey => _reCaptchaSettings.SiteKey;


        public class InputModel
        {
            [Required(ErrorMessage = "Le montant est obligatoire.")]
            [Range(500.0, 15000.0, ErrorMessage = "Le montant doit être compris entre 500 et 15 000 HTG.")]
            public decimal Amount { get; set; }

            [Required(ErrorMessage = "Le numéro est obligatoire")]
            [RegularExpression(@"^\d{8}$", ErrorMessage = "Numero Incorrect.")]
            [StringLength(8, ErrorMessage = "Numero incorrect.")]
            public string Number { get; set; }
            public string CodeNumBer { get; set; }

            [Required(ErrorMessage = "Le mot de passe est requis")]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public void OnGet()
        {
            if (!_monCashService.ArePayoutsEnabled)
            {
                StatusMessage = "Les retraits MonCash sont temporairement désactivés pendant la sécurisation du service.";
                return;
            }

            // Obtenez l'utilisateur actuel
            CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (CurrentUser != null)
            {
                CurrentWallet = _walletService.GetOrCreateWalletAsync(CurrentUser.Id, WalletType.Earnings).GetAwaiter().GetResult();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!_monCashService.ArePayoutsEnabled)
            {
                ModelState.AddModelError(string.Empty,
                    "Les retraits MonCash sont temporairement désactivés pendant la sécurisation du service.");
                return Page();
            }

            // Récupérer l'utilisateur et son portefeuille
            CurrentUser = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (CurrentUser == null)
            {
                ModelState.AddModelError(string.Empty, "Utilisateur introuvable.");
                return Page();
            }

            CurrentWallet = await _walletService.GetOrCreateWalletAsync(CurrentUser.Id, WalletType.Earnings);
            if (CurrentWallet == null)
            {
                ModelState.AddModelError(string.Empty, "Portefeuille introuvable.");
                return Page();
            }
            string number = Input.CodeNumBer + Input.Number;

            // Vérification reCAPTCHA
            var reCaptchaToken = Request.Form["g-recaptcha-response"];
            if (!await _reCaptchaService.VerifyAsync(reCaptchaToken))
            {
                ModelState.AddModelError(string.Empty, "La vérification reCAPTCHA a échoué. Veuillez réessayer.");
                return Page();
            }


            // Vérification si le modèle est valide
            if (ModelState.IsValid)
            {
                // Vérifiez le mot de passe de l'utilisateur
                var user = await _userManager.FindByNameAsync(CurrentUser.UserName);
                if (user == null || !await _userManager.CheckPasswordAsync(user, Input.Password))
                {
                    ModelState.AddModelError(string.Empty, "Mot de passe incorrect.");
                    return Page();
                }

                MonCashService.CustomerStatus customerStatus;
                try
                {
                    customerStatus = await _monCashService.CheckCustomerStatusAsync(number);
                }
                catch
                {
                    ModelState.AddModelError(string.Empty,
                        "Le statut du compte MonCash ne peut pas être vérifié pour le moment.");
                    return Page();
                }

                var accountStatuses = customerStatus.CustomerStatusDetails?.Status ?? Array.Empty<string>();
                var isEligibleForPayout = customerStatus.Status == StatusCodes.Status200OK &&
                    string.Equals(customerStatus.CustomerStatusDetails?.Type, "fullkyc", StringComparison.OrdinalIgnoreCase) &&
                    accountStatuses.Contains("registered", StringComparer.OrdinalIgnoreCase) &&
                    accountStatuses.Contains("active", StringComparer.OrdinalIgnoreCase);

                if (!isEligibleForPayout)
                {
                    ModelState.AddModelError(string.Empty,
                        "Le compte MonCash doit être enregistré, actif et entièrement vérifié pour recevoir un retrait.");
                    return Page();
                }

                string processedTransactionId = null;
                try
                {
                    decimal feeRate = 0.03m; // 3% de frais
                    decimal totalAmount = Input.Amount + (Input.Amount * feeRate);

                    // SÉCURITÉ CORRIGÉE: Utiliser le service financier sécurisé
                    var result = await _secureFinancialService.ProcessWithdrawalAsync(
                        CurrentUser.Id, 
                        totalAmount, 
                        TransactionMethod.MonCash, 
                        $"Retrait MonCash - Frais {feeRate:P0} inclus",
                        WalletType.Earnings
                    );

                    if (result.Success)
                    {
                        processedTransactionId = result.TransactionId;

                        if (!int.TryParse(result.TransactionId, out var localTransactionId))
                        {
                            throw new InvalidOperationException("L'identifiant local du retrait est invalide.");
                        }

                        var withdrawalTransaction = await _context.Transactions
                            .FirstOrDefaultAsync(t => t.Id == localTransactionId);
                        if (withdrawalTransaction == null)
                        {
                            throw new InvalidOperationException("La transaction locale du retrait est introuvable.");
                        }

                        withdrawalTransaction.Status = TransactionStatus.Processing;
                        await _context.SaveChangesAsync();

                        // Appel de la méthode SendPayoutAsync
                        var transferResponse = await _monCashService.SendPayoutAsync(Input.Amount, number, 
                            $"Retrait sur MCBusiness de l'utilisateur {CurrentUser.UserName}", result.OrderId);

                        // Vérifier le Status du transfert
                        if (transferResponse?.Transfer?.Message == "successful")
                        {
                            withdrawalTransaction.Status = TransactionStatus.Completed;
                            withdrawalTransaction.TransactionId = transferResponse.Transfer.TransactionId;
                            withdrawalTransaction.CompletedAt = DateTimeOffset.UtcNow;
                            await _context.SaveChangesAsync();

                            StatusMessage = "Le transfert a été effectué avec succès.";
                            TempData["SuccessMessage"] = $"Retrait de {Input.Amount} HTG effectué avec succès!";
                        }
                        else
                        {
                            StatusMessage = "Le retrait est en cours de vérification auprès de MonCash.";
                            TempData["ErrorMessage"] =
                                "La confirmation MonCash est incertaine. Aucun remboursement automatique n'a été effectué afin d'éviter un double paiement.";
                        }

                        return RedirectToPage("/Users/User/Transaction");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = result.ErrorMessage;
                    }
                }
                catch (Exception)
                {
                    StatusMessage = !string.IsNullOrEmpty(processedTransactionId)
                        ? "Le retrait est en cours de vérification auprès de MonCash. Aucun remboursement automatique n'a été effectué."
                        : "Le retrait ne peut pas être initié pour le moment.";

                }

            }

            // Retourner la page avec les erreurs si le modèle est invalide
            return Page();
        }
    }
}
