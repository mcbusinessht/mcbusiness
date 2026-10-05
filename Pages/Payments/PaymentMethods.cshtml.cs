using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations; 
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MCBusiness.Attributes;
using MCBusiness.Services;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
namespace MCBusiness.Pages.Payments
{
    [Authorize]
    [MCBusiness.Attributes.IdempotentAction(timeoutSeconds: 10, keyPrefix: "PaymentMethods")]
    public class PaymentMethodsModel : PageModel
    {   
        private readonly MonCashService _monCashService;
        private readonly ILogger<PaymentMethodsModel> _logger;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;


        private readonly MCBusiness.Services.INotificationService _notificationService;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public PaymentMethodsModel(
            ILogger<PaymentMethodsModel> logger, 
            MonCashService monCashService, 
            AppDbContext context, 
            IWebHostEnvironment env,
            MCBusiness.Services.INotificationService notificationService,
            Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _monCashService = monCashService;
            _context= context;
            _env= env;
            _notificationService = notificationService;
            _userManager = userManager;
        }

        [BindProperty]
        public string PaymentLink { get; set; }

        [BindProperty]
        public string ErrorMessage { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(50.0, 20000.0, ErrorMessage = "Le montant doit être compris entre 50 et 20 000 HTG.")]
        public decimal Amount { get; set; }

        [BindProperty]
        public BankTransferInputModel BankTransferInput { get; set; }


        public ApplicationUser CurrentUser{get; set;}
        public List<Currency> AvailableCurrencies { get; set; } = new();

        private async Task LoadAvailableCurrenciesAsync()
        {
            AvailableCurrencies = await _context.Currencies
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }
        
        public IActionResult OnGet()
        {
            CurrentUser = _context.Users.FirstOrDefault(u=>u.UserName == User.Identity.Name);
            if(CurrentUser == null){
                return Redirect("/Account/Login");
            }
            
            // Charger les devises disponibles
            AvailableCurrencies = _context.Currencies
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Code)
                .ToList();
                
            return Page();
        }

        public async Task<IActionResult> OnPostMonCashPaymentAsync()
        {
            _logger.LogInformation("Soumission d'un dépôt MonCash reçue");
            await LoadAvailableCurrenciesAsync();

            // La page contient deux formulaires indépendants. Les champs requis du
            // virement bancaire ne doivent pas invalider une soumission MonCash.
            foreach (var key in ModelState.Keys
                         .Where(key => key.Equals(nameof(BankTransferInput), StringComparison.OrdinalIgnoreCase) ||
                                       key.StartsWith($"{nameof(BankTransferInput)}.", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                ModelState.Remove(key);
            }

            if (Amount < 50m || Amount > 20000m || decimal.Round(Amount, 2) != Amount)
            {
                _logger.LogWarning("Dépôt MonCash refusé par la validation du montant");
                if (Amount < 50m || Amount > 20000m)
                    ModelState.AddModelError(nameof(Amount),
                        "Le montant doit être compris entre 50 et 20 000 HTG.");
                else
                    ModelState.AddModelError(nameof(Amount), "Le montant ne peut pas contenir plus de deux décimales.");
                return Page();
            }

            var orderId= GenerateOrderId();
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (CurrentUser == null)
                return Challenge();

            var currency = _context.Currencies
                .FirstOrDefault(c => c.Code == "HTG");
                
            if (currency == null)
                throw new Exception("Devise HTG non trouvée");
            var Transaction = new Transaction
            {
                UserId = userId,
                Amount = Amount,
                Type = TransactionType.Deposit,
                WalletType = WalletType.Customer,
                Method = TransactionMethod.MonCash,
                Description = "Depot sur MCBusinessWallet",
                Status = TransactionStatus.Pending,
                CurrencyBase = currency.Id,
                OrderId= orderId
            };         
            _context.Transactions.Add(Transaction);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Transaction MonCash locale {OrderId} créée en attente", orderId);


            try
            {
                var totalAmount = MonCashPricing.CalculateDepositGross(Amount);
                _logger.LogInformation("Initialisation du paiement MonCash {OrderId}", orderId);
                PaymentLink = await _monCashService.CreatePaymentAsync(totalAmount, orderId);
            }
            catch (Exception ex)
            {
                Transaction.Status = TransactionStatus.Failed;
                await _context.SaveChangesAsync();
                _logger.LogError(ex, "Échec de l'initialisation du dépôt MonCash {OrderId}", orderId);
                ModelState.AddModelError(string.Empty, "Le paiement MonCash ne peut pas être initialisé pour le moment.");
            }
            if(PaymentLink !=null){
                
                return Redirect(PaymentLink);
            }

            return Page();
            

        }
        public async Task<IActionResult> OnPostBankTransferAsync()
        {
            await LoadAvailableCurrenciesAsync();

            ModelState.Remove(nameof(Amount));
            if (!ModelState.IsValid)
                return Page();

            // Récupérer l'user
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Enregistrer le fichier
            var proof = BankTransferInput.ProofFile;
            var uploads = Path.Combine(_env.WebRootPath, "uploads", "bankproofs");
            Directory.CreateDirectory(uploads);
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(proof.FileName)}";
            var filePath = Path.Combine(uploads, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await proof.CopyToAsync(stream);

            
            var transaction = new Transaction
            {
                UserId        = userId,
                Amount        = BankTransferInput.Amount,
                Type          = TransactionType.Deposit,
                WalletType    = WalletType.Customer,
                Method        = TransactionMethod.BankTransfer,
                Description   = $"Virement bancaire '{BankTransferInput.TransferMethod}' depuis mon compte {BankTransferInput.AccountNumber}",
                Status        = TransactionStatus.Pending,
                CurrencyBase  = (await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "HTG")).Id,
                OrderId       = GenerateOrderId(),
            };
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();             
            // Créer l'entité
            var payment = new BankTransferPayment
            {
                UserId        = userId,
                BankName      = BankTransferInput.BankName,
                AccountName   = BankTransferInput.AccountName,
                AccountNumber = BankTransferInput.AccountNumber,
                TransferMethod  = BankTransferInput.TransferMethod,
                Direction       = BankTransferDirection.Deposit, // Assuming deposit direction
                Amount        = BankTransferInput.Amount,
                CurrencyId    = BankTransferInput.CurrencyId,
                ProofFilePath = $"/uploads/bankproofs/{fileName}",
                Status        = BankTransferStatus.Pending,
                TransactionId = transaction.Id,
                WalletType    = WalletType.Customer,
                SubmittedAt   = DateTimeOffset.UtcNow
            };
            _context.BankTransferPayments.Add(payment);

            await _context.SaveChangesAsync();

           // Préparer l'affichage du nom d'utilisateur dans la notification
           var userNameForDisplay = User?.Identity?.Name ?? userId;

           // NOTIFICATIONS ADMIN: créer une notification pour chaque utilisateur ayant le rôle Admin
           var admins = await _userManager.GetUsersInRoleAsync("Admin");
           foreach (var admin in admins)
           {
               await _notificationService.NotifyAsync(new Notification
               {
                   UserId = admin.Id,
                   Title = "Nouveau dépôt par virement bancaire",
                   Message = $"L'utilisateur {userNameForDisplay} a soumis un dépôt de {BankTransferInput.Amount:N2} HTG. Veuillez vérifier le statut dans l'Admin.",
                   IsRead = false,
                   CreatedAt = DateTimeOffset.UtcNow
               });
           }

           // Notification à l'utilisateur (accusé de réception)
           await _notificationService.NotifyAsync(new Notification
           {
               UserId = userId,
               Title = "Dépôt soumis",
               Message = $"Votre dépôt par virement bancaire a été soumis. Suivez l'état ici: /Payments/PaymentMethods",
               IsRead = false,
               CreatedAt = DateTimeOffset.UtcNow
           });

           TempData["Success"] = "Virement soumis - en attente de validation.";
           return RedirectToPage("/Users/User/Transaction");
        }

        public string GenerateOrderId()
        {
            string orderId;
            Transaction transactionVerifyOrderId = null;
            do{
                // Générer un nouvel ID de commande aléatoire
                orderId = GenerateSecureNumericId();
                // Vérifier si l'ID de commande existe déjà dans la base de données
                transactionVerifyOrderId=_context.Transactions.FirstOrDefault(t=>t.OrderId==orderId);
            }while(transactionVerifyOrderId != null);
            return orderId;
        }

        private static string GenerateSecureNumericId()
        {
            var bytes = RandomNumberGenerator.GetBytes(sizeof(ulong));
            var value = BitConverter.ToUInt64(bytes, 0);
            return (100000000000UL + (value % 900000000000UL)).ToString();
        }
    }

    public class BankTransferInputModel
    {
        [Required, MaxLength(100)]
        public string BankName { get; set; }

        [Required, MaxLength(100)]
        public string AccountName { get; set; }

        [Required, MaxLength(50)]
        public string AccountNumber { get; set; }

        [Required]
        public BankTransferMethod TransferMethod { get; set; }

        [Required, Range(1, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Veuillez sélectionner une devise")]
        public int CurrencyId { get; set; }

        [Required]
        [DataType(DataType.Upload)]
        [MaxFileSize(5 * 1024 * 1024)]        // 5 MB max (attribut custom si implémenté)
        [AllowedExtensions(new string[] { ".jpg", ".png", ".pdf" })]
        public IFormFile ProofFile { get; set; }
    }
}
