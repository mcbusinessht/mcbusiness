using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Interfaces;


namespace MCBusiness.Pages.Payments
{
    public class ThankYouModel : PageModel
    {
        private readonly ILogger<ThankYouModel> _logger;
        private readonly MonCashService _monCashService;
        private AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly MCBusiness.Services.ISecureFinancialService _secureFinancialService;

        public ThankYouModel(
            ILogger<ThankYouModel> logger,
            MonCashService monCashService,
            AppDbContext context,
            IEmailSender emailSender,
            MCBusiness.Services.ISecureFinancialService secureFinancialService)
        {
            _logger = logger;
            _monCashService = monCashService;
            _context= context;
            _emailSender= emailSender;
            _secureFinancialService = secureFinancialService;
        }

        public string TransactionId { get; set; }
        public MonCashService.PaymentDetails PaymentDetails { get; set; }
        public Transaction Transaction{get; set;}
        public Wallet Wallet {get; set;}
        public string StatusMessage { get; set; }
        public ApplicationUser CurrentUser{get; set;}


        public async Task<IActionResult> OnGetAsync()
        {
            // Récupérer l'ID de transaction depuis les paramètres de la requête
            TransactionId = Request.Query["transactionId"];
            if (string.IsNullOrEmpty(TransactionId))
            {
                StatusMessage = "Aucun ID de transaction fourni.";
                return Page();
            }

            try
            {
                var existingTransaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == TransactionId);

                if (existingTransaction != null)
                {
                    StatusMessage = "Cette transaction a déjà été enregistrée.";
                    return Redirect("./MonCash");
                }

                
                // Récupérer les détails du paiement via MonCash
                PaymentDetails = await _monCashService.RetrievePaymentByTransactionIdAsync(TransactionId);

                if (PaymentDetails != null && PaymentDetails.Payment != null)
                {
                    // Récupérer l'utilisateur courant
                    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    CurrentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                    if (CurrentUser == null)
                    {
                        StatusMessage = "Utilisateur introuvable.";
                        return Page();
                    }

                    // Vérifier le Status du paiement
                    StatusMessage = PaymentDetails.Status == StatusCodes.Status200OK &&
                                    string.Equals(PaymentDetails.Payment.Message, "successful", StringComparison.OrdinalIgnoreCase)
                        ? "Paiement reçu par MonCash. La confirmation de votre portefeuille est en cours."
                        : $"Le paiement a échoué. Message : {PaymentDetails.Payment.Message}";
                            
                        
                    }
                    else
                    {
                        StatusMessage = $"Le paiement a échoué. Message : {PaymentDetails.Payment.Message}";
                    }
                }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Transaction non trouvée : {TransactionId}");
                StatusMessage = "La transaction n'a pas été trouvée. Veuillez vérifier l'ID de transaction.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification du paiement : {ex.Message}");
                StatusMessage = "Une erreur est survenue lors de la vérification du paiement.";
            }

            return Page();
        }
    }
}
