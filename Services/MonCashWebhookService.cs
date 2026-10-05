using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;
using MCBusiness.Services;
using MCBusiness.Interfaces;
using System.Text.RegularExpressions;

namespace MCBusiness.Services
{
    public class MonCashWebhookService
    {
        private readonly ILogger<MonCashWebhookService> _logger;
        private readonly AppDbContext _context;
        private readonly MonCashService _monCashService;
        private readonly ISecureFinancialService _secureFinancialService;

        public MonCashWebhookService(
            ILogger<MonCashWebhookService> logger,
            AppDbContext context,
            MonCashService monCashService,
            ISecureFinancialService secureFinancialService)
        {
            _logger = logger;
            _context = context;
            _monCashService = monCashService;
            _secureFinancialService = secureFinancialService;
        }

        public async Task HandleWebhookAsync(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store, private";
            _logger.LogInformation("Notification MonCash reçue");
            try
            {
                // ? 1. Récupérer transactionId depuis l'URL
                string transactionId = context.Request.Query["transactionId"];
                if (string.IsNullOrWhiteSpace(transactionId) ||
                    !Regex.IsMatch(transactionId, "^[0-9]{1,64}$", RegexOptions.CultureInvariant))
                {
                    _logger.LogWarning("Identifiant de transaction MonCash absent ou invalide");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Identifiant de transaction invalide.");
                    return;
                }

                _logger.LogInformation("Notification MonCash reçue avec un identifiant de transaction");

                // ? 2. Vérifier si la transaction existe déjà
                var existingTransaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

                if (existingTransaction != null)
                {
                    _logger.LogInformation("?? Cette transaction a déjà été enregistrée.");
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsync("Cette transaction a déjà été enregistrée.");
                    return;
                }

                // ? 3. Récupérer les détails du paiement via MonCash
                var paymentDetails = await _monCashService.RetrievePaymentByTransactionIdAsync(transactionId);
                if (paymentDetails == null || paymentDetails.Payment == null)
                {
                    _logger.LogWarning("?? Impossible de récupérer les détails du paiement.");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Les détails du paiement sont introuvables.");
                    return;
                }

                if (paymentDetails.Status != StatusCodes.Status200OK ||
                    !string.Equals(paymentDetails.Payment.TransactionId, transactionId, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Réponse MonCash incohérente pour une notification reçue");
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsync("Réponse MonCash incohérente.");
                    return;
                }

                // ? 4. Vérifier si la transaction est réussie
                if (!string.Equals(paymentDetails.Payment.Message, "successful", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("? Le paiement a échoué.");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Le paiement n'est pas confirmé.");
                    return;
                }

                var orderId = paymentDetails.Payment.Reference;
                var settlement = await _secureFinancialService.CompletePendingDepositAsync(
                    orderId,
                    transactionId,
                    paymentDetails.Payment.Cost);

                if (!settlement.Success)
                {
                    context.Response.StatusCode = 409;
                    await context.Response.WriteAsync(settlement.ErrorMessage);
                    return;
                }

                _logger.LogInformation("? Transaction enregistrée avec succès.");
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("Transaction enregistrée avec succès.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement de la notification MonCash");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Erreur interne du serveur.");
            }
        }
    }
}
