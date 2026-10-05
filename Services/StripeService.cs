using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;
using MCBusiness.Models;
using MCBusiness.Services;



namespace MCBusiness.Services
{
    public class StripeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly StripeConfig _stripeConfig;
        private readonly IPlatformTransactionAuditService _auditService;
        private const string BaseUrl = "https://api.stripe.com/v1";

        public StripeService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IPlatformTransactionAuditService auditService)
        {
            _httpClientFactory = httpClientFactory;
            _stripeConfig = configuration.GetSection("Stripe").Get<StripeConfig>();
            _auditService = auditService;
            if (string.IsNullOrEmpty(_stripeConfig.SecretKey))
                throw new Exception("Stripe SecretKey manquant en configuration.");
        }

        // --- Helper pour créer un client avec auth Bearer ---
        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(BaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _stripeConfig.SecretKey);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
            return client;
        }

        // 1) Créer un PaymentIntent (paiement carte / mobile / international)
        public async Task<StripePaymentIntent> CreatePaymentIntentAsync(
            long amountInCents,
            string currency,
            string customerId = null,
            IList<string> paymentMethodTypes = null,
            Dictionary<string,string> metadata = null)
        {
            var client = CreateClient();
            var form = new List<KeyValuePair<string,string>>
            {
                new("amount", amountInCents.ToString()),
                new("currency", currency)
            };

            // types (default ["card"])
            if (paymentMethodTypes == null) paymentMethodTypes = new List<string>{ "card" };
            for(int i=0;i<paymentMethodTypes.Count;i++)
                form.Add(new KeyValuePair<string,string>($"payment_method_types[{i}]", paymentMethodTypes[i]));

            if (!string.IsNullOrEmpty(customerId))
                form.Add(new ("customer", customerId));

            if (metadata != null)
            {
                foreach(var kv in metadata)
                    form.Add(new($"metadata[{kv.Key}]", kv.Value));
            }

            var content = new FormUrlEncodedContent(form);
            var res = await client.PostAsync("/payment_intents", content);
            var json = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<StripePaymentIntent>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }

        // 2) Récupérer un PaymentIntent existant
        public async Task<StripePaymentIntent> RetrievePaymentIntentAsync(string paymentIntentId)
        {
            var client = CreateClient();
            var res = await client.GetAsync($"/payment_intents/{paymentIntentId}");
            var json = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<StripePaymentIntent>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }

        // 3) Créer un Payout (virement vers compte bancaire ou carte)
        public async Task<StripePayout> CreatePayoutAsync(long amountInCents, string currency, string destinationAccountId)
        {
            var client = CreateClient();
            var form = new List<KeyValuePair<string,string>>
            {
                new("amount", amountInCents.ToString()),
                new("currency", currency),
                new("destination", destinationAccountId)
            };
            var content = new FormUrlEncodedContent(form);
            var res = await client.PostAsync("/payouts", content);
            var json = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<StripePayout>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }

        // 4) Créer un Customer dans Stripe (stocke cartes, mobile wallets)
        public async Task<StripeCustomer> CreateCustomerAsync(string email, string name = null)
        {
            var client = CreateClient();
            var form = new List<KeyValuePair<string,string>>
            {
                new("email", email)
            };
            if (!string.IsNullOrEmpty(name))
                form.Add(new ("name", name));

            var content = new FormUrlEncodedContent(form);
            var res = await client.PostAsync("/customers", content);
            var json = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<StripeCustomer>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }

        // 5) Créer une Subscription (facturation récurrente)
        public async Task<StripeSubscription> CreateSubscriptionAsync(string customerId, string priceId, int trialDays = 0)
        {
            var client = CreateClient();
            var form = new List<KeyValuePair<string,string>>
            {
                new("customer", customerId),
                new("items[0][price]", priceId)
            };
            if (trialDays > 0)
                form.Add(new("trial_period_days", trialDays.ToString()));

            var content = new FormUrlEncodedContent(form);
            var res = await client.PostAsync("/subscriptions", content);
            var json = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<StripeSubscription>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }

        // 6) Créer une Checkout Session (paiement hébergé)
        public async Task<StripeCheckoutSession> CreateCheckoutSessionAsync(
            IList<(string priceId,int quantity)> items,
            string successUrl,
            string cancelUrl,
            string customerEmail = null)
        {
            var client = CreateClient();
            var form = new List<KeyValuePair<string,string>>
            {
                new("mode", "payment"),
                new("success_url", successUrl),
                new("cancel_url", cancelUrl),
            };
            if (!string.IsNullOrEmpty(customerEmail))
                form.Add(new("customer_email", customerEmail));

            for(int i=0;i<items.Count;i++)
            {
                form.Add(new($"line_items[{i}][price]", items[i].priceId));
                form.Add(new($"line_items[{i}][quantity]", items[i].quantity.ToString()));
            }

            var content = new FormUrlEncodedContent(form);
            var res = await client.PostAsync("/checkout/sessions", content);
            var json = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<StripeCheckoutSession>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }

        // 7) Confirmer un paiement et enregistrer l'audit (NOUVEAU - CRITIQUE)
        public async Task ConfirmPaymentAndAuditAsync(int orderId, string paymentIntentId, decimal amount)
        {
            try
            {
                // Vérifier le statut du PaymentIntent
                var paymentIntent = await RetrievePaymentIntentAsync(paymentIntentId);
                
                if (paymentIntent.Status == "succeeded")
                {
                    // AUDIT CRITIQUE : Enregistrer le revenu dès confirmation
                    await _auditService.RecordOrderRevenueAsync(
                        orderId, 
                        amount, 
                        $"Paiement Stripe confirmé - PaymentIntent: {paymentIntentId}"
                    );
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la confirmation et audit du paiement : {ex.Message}");
            }
        }
    }

    // --- Modèles de réponse (simplifiés) ---
    public class StripePaymentIntent
    {
        public string Id { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string ClientSecret { get; set; }
    }

    public class StripePayout
    {
        public string Id { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
    }

    public class StripeCustomer
    {
        public string Id { get; set; }
        public string Email { get; set; }
    }

    public class StripeSubscription
    {
        public string Id { get; set; }
        public string Status { get; set; }
    }

    public class StripeCheckoutSession
    {
        public string Id { get; set; }
        public string Url { get; set; }
    }
}
