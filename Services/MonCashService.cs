using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Text.Json.Serialization;
using MCBusiness.Services; 
public class MonCashService

{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MonCashConfig _monCashConfig;
    private readonly IPlatformTransactionAuditService _auditService;
    private readonly string _baseUrl;

    public bool IsEnabled => _monCashConfig.Enabled;
    public bool ArePayoutsEnabled => _monCashConfig.Enabled && _monCashConfig.EnablePayouts;
    public string ReturnUrl => _monCashConfig.ReturnUrl;
    public string CallbackUrl => _monCashConfig.CallbackUrl;

    public MonCashService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IPlatformTransactionAuditService auditService)
    {
        _httpClientFactory = httpClientFactory;
        _auditService = auditService;

        // Charger la configuration "MonCash" depuis appsettings.json
        _monCashConfig = configuration.GetSection("MonCash").Get<MonCashConfig>();
        if (_monCashConfig == null)
        {
            throw new InvalidOperationException("La configuration MonCash est absente.");
        }

        var monCashEnvironment = _monCashConfig.Environment?.Trim();
        _baseUrl = monCashEnvironment?.ToLowerInvariant() switch
        {
            "sandbox" => "https://sandbox.moncashbutton.digicelgroup.com",
            "production" or "live" => "https://moncashbutton.digicelgroup.com",
            _ => throw new InvalidOperationException(
                "MonCash:Environment doit être Sandbox ou Production.")
        };

        if (!_monCashConfig.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(_monCashConfig.ClientId) ||
            string.IsNullOrWhiteSpace(_monCashConfig.ClientSecret))
        {
            throw new InvalidOperationException(
                "MonCash est activé mais ClientId ou ClientSecret est absent.");
        }

        if (_baseUrl == "https://moncashbutton.digicelgroup.com" && !_monCashConfig.AllowProduction)
        {
            throw new InvalidOperationException(
                "Les appels MonCash de production sont bloqués. Activez explicitement MonCash:AllowProduction après validation du staging.");
        }

        ValidateApplicationUrl(_monCashConfig.ReturnUrl, "ReturnUrl");
        ValidateApplicationUrl(_monCashConfig.CallbackUrl, "CallbackUrl");
    }

    private static void ValidateApplicationUrl(string value, string settingName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"MonCash:{settingName} doit être une URL HTTPS absolue sans identifiants ni fragment.");
        }
    }

    public async Task<string> CreatePaymentAsync(decimal amount, string orderId)
    {
        try
        {
            // Étape 1 : Obtenir le token d'accès
            var accessToken = await GetAccessTokenAsync();

            if (string.IsNullOrEmpty(accessToken))
                throw new Exception("Impossible d'obtenir le token d'accès.");

            // Étape 2 : Créer le paiement
            return await GeneratePaymentLinkAsync(accessToken, amount, orderId);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erreur lors de la création du paiement : {ex.Message}");
        }
    }

    // private async Task<string> GetAccessTokenAsync()
    // {
    //     var authUrl = $"{ProductionUrl}/Api/oauth/token";

    //     using (var client = _httpClientFactory.CreateClient())
    //     {
    //         // Authentification Basic Auth : ajout des informations dans l'en-tête
    //         string basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_monCashConfig.ClientId}:{_monCashConfig.ClientSecret}"));
    //         client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

    //         // Corps de la requête avec grant_type et scope
    //         var authData = new StringContent(
    //             "grant_type=client_credentials&scope=read,write",
    //             Encoding.UTF8,
    //             "application/x-www-form-urlencoded"
    //         );

    //         var response = await client.PostAsync(authUrl, authData);

    //         if (response.IsSuccessStatusCode)
    //         {
    //             var responseContent = await response.Content.ReadAsStringAsync();
    //             var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

    //             // Vérifier si la clé "access_token" existe
    //             if (jsonResponse.TryGetProperty("access_token", out var accessTokenProperty))
    //             {
    //                 return accessTokenProperty.GetString();
    //             }
    //             else
    //             {
    //                 throw new Exception("Le token d'accès n'a pas été trouvé dans la réponse.");
    //             }
    //         }
    //         else
    //         {
    //             var errorContent = await response.Content.ReadAsStringAsync();
    //             throw new Exception($"Erreur lors de l'obtention du token : {response.StatusCode} - {errorContent}");
    //         }
    //     }
    // }

    private async Task<string> GeneratePaymentLinkAsync(string accessToken, decimal amount, string orderId)
    {
        var paymentUrl = $"{_baseUrl}/Api/v1/CreatePayment";
        var gatewayBase = $"{_baseUrl}/Moncash-middleware";

        using (var client = _httpClientFactory.CreateClient("MonCash"))
        {
            // Ajouter le token d'accès dans les en-têtes de la requête
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            // Construire les données du paiement
            var paymentData = new
            {
                amount = amount,
                orderId = orderId
            };

            var paymentJson = new StringContent(
                JsonSerializer.Serialize(paymentData),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync(paymentUrl, paymentJson);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (!jsonResponse.TryGetProperty("status", out var statusProperty) ||
                    statusProperty.GetInt32() != StatusCodes.Status202Accepted)
                {
                    throw new InvalidOperationException("MonCash a retourné un statut de création inattendu.");
                }

                var expectedMode = _baseUrl.Contains("sandbox.", StringComparison.OrdinalIgnoreCase)
                    ? "sandbox"
                    : "live";
                if (!jsonResponse.TryGetProperty("mode", out var modeProperty) ||
                    !string.Equals(modeProperty.GetString(), expectedMode, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Le mode de la réponse MonCash ne correspond pas à l'environnement configuré.");
                }

                if (jsonResponse.TryGetProperty("payment_token", out var paymentTokenProperty) &&
                    paymentTokenProperty.TryGetProperty("token", out var tokenProperty) &&
                    !string.IsNullOrWhiteSpace(tokenProperty.GetString()))
                {
                    return $"{gatewayBase}/Payment/Redirect?token={tokenProperty.GetString()}";
                }
                else
                {
                    throw new Exception("Le token de paiement n'a pas été trouvé dans la réponse.");
                }
            }
            else
            {
                throw new HttpRequestException(
                    $"MonCash a refusé la création du paiement ({(int)response.StatusCode}).",
                    null,
                    response.StatusCode);
            }
        }
        
    }
     // Vérification d'un paiement par ID de transaction
public async Task<PaymentDetails> RetrievePaymentByTransactionIdAsync(string transactionId)
{
    var accessToken = await GetAccessTokenAsync();

    // URL de l'API
    var url = $"{_baseUrl}/Api/v1/RetrieveTransactionPayment";

    // Corps de la requête
    var payload = new { transactionId };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    using (var client = _httpClientFactory.CreateClient("MonCash"))
    {
        // Ajouter les en-têtes
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            // Envoyer la requête POST
            var response = await client.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var paymentDetails = JsonSerializer.Deserialize<PaymentDetails>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Gestion des noms de propriété insensibles à la casse
                    });

                    if (paymentDetails == null || paymentDetails.Payment == null)
                    {
                        throw new Exception("Les détails du paiement sont introuvables ou incomplets.");
                    }

                    // Log facultatif pour le timestamp
                    if (paymentDetails.Timestamp > 0)
                    {
                        var transactionDate = DateTimeOffset.FromUnixTimeMilliseconds(paymentDetails.Timestamp);
                    }

                    return paymentDetails;
                }
                catch (JsonException ex)
                {
                    throw new Exception("La réponse de l'API n'est pas au format attendu.");
                }
            }
            else
            {
                throw new HttpRequestException(
                    $"MonCash a refusé la récupération de la transaction ({(int)response.StatusCode}).",
                    null,
                    response.StatusCode);
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception("Une erreur s'est produite lors du traitement de la demande.");
        }
    }
}

    // Confirmer un paiement MonCash et enregistrer l'audit (NOUVEAU - CRITIQUE)
    public async Task ConfirmPaymentAndAuditAsync(int orderId, string transactionId, decimal amount)
    {
        try
        {
            // Vérifier le statut du paiement MonCash
            var paymentDetails = await RetrievePaymentByTransactionIdAsync(transactionId);
            
            // Statut 200 = paiement réussi dans MonCash
            if (paymentDetails.Status == 200 && paymentDetails.Payment != null)
            {
                // AUDIT CRITIQUE : Enregistrer le revenu dès confirmation
                await _auditService.RecordOrderRevenueAsync(
                    orderId, 
                    amount, 
                    $"Paiement MonCash confirmé - Transaction: {transactionId}"
                );
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Erreur lors de la confirmation et audit du paiement MonCash : {ex.Message}");
        }
    }


    // Vérification d'un paiement par ID de commande
    // Vérification d'un paiement par ID de commande
    public async Task<PaymentDetails> RetrievePaymentByOrderIdAsync(string orderId)
    {
        var accessToken = await GetAccessTokenAsync();

        // URL de l'API
        var url = $"{_baseUrl}/Api/v1/RetrieveOrderPayment";

        // Corps de la requête
        var payload = new { orderId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using (var client = _httpClientFactory.CreateClient("MonCash"))
        {
            // Ajouter les en-têtes
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                // Envoyer la requête POST
                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var paymentDetails = JsonSerializer.Deserialize<PaymentDetails>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true // Gestion des noms de propriété insensibles à la casse
                        });

                        if (paymentDetails == null || paymentDetails.Payment == null)
                        {
                            throw new Exception("Les détails du paiement sont introuvables ou incomplets.");
                        }

                        // Log facultatif pour le timestamp
                        if (paymentDetails.Timestamp > 0)
                        {  
                            var transactionDate = DateTimeOffset.FromUnixTimeMilliseconds(paymentDetails.Timestamp);
                        }

                        return paymentDetails;
                    }
                    catch (JsonException ex)
                    {
                        throw new Exception("La réponse de l'API n'est pas au format attendu.");
                    }
                }
                else
                {
                    throw new HttpRequestException(
                        $"MonCash a refusé la récupération de la commande ({(int)response.StatusCode}).",
                        null,
                        response.StatusCode);
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Une erreur s'est produite lors du traitement de la demande.");
            }
        }
    }


    // Vérification du statut d'un client
   // Vérification du statut d'un client
   public async Task<CustomerStatus> CheckCustomerStatusAsync(string account)
{
    var accessToken = await GetAccessTokenAsync();

    // URL de l'API
    var url = $"{_baseUrl}/Api/v1/CustomerStatus";

    // Corps de la requête
    var payload = new { account };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    using (var client = _httpClientFactory.CreateClient("MonCash"))
    {
        // Ajouter les en-têtes
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            // Envoyer la requête POST
            var response = await client.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var customerStatus = JsonSerializer.Deserialize<CustomerStatus>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // Gestion des noms de propriété insensibles à la casse
                    });

                    if (customerStatus == null || customerStatus.CustomerStatusDetails == null)
                    {
                        throw new Exception("Les détails du statut client sont introuvables ou incomplets.");
                    }

                    return customerStatus;
                }
                catch (JsonException ex)
                {
                    throw new Exception("La réponse de l'API n'est pas au format attendu.");
                }
            }
            else
            {
                throw new HttpRequestException(
                    $"MonCash a refusé la vérification du compte ({(int)response.StatusCode}).",
                    null,
                    response.StatusCode);
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception("Une erreur s'est produite lors du traitement de la demande.");
        }
    }
}
   // Envoi d'un paiement préfinancé--------------------------------------------------------------------------------------------------------
    public async Task<TransferResponse> SendPayoutAsync(decimal amount, string receiver, string desc, string reference)
{
    if (!_monCashConfig.EnablePayouts)
        throw new InvalidOperationException("Les retraits MonCash sont temporairement désactivés.");

    var accessToken = await GetAccessTokenAsync();

    // URL de l'API pour le transfert
    var url = $"{_baseUrl}/Api/v1/Transfert";

    // Corps de la requête
    var payload = new { amount, receiver, desc, reference };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    using (var client = _httpClientFactory.CreateClient("MonCash"))
    {
        // Ajouter les en-têtes
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Envoyer la requête POST
        var response = await client.PostAsync(url, content);

        // Lire la réponse brute pour journalisation
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            try
            {
                // Désérialiser la réponse en `TransferResponse`
                var transferResponse = JsonSerializer.Deserialize<TransferResponse>(responseContent, MonCashJsonOptions)
                    ?? throw new JsonException("La réponse MonCash est vide ou invalide.");

                if (transferResponse.Status != StatusCodes.Status200OK ||
                    transferResponse.Transfer == null ||
                    string.IsNullOrWhiteSpace(transferResponse.Transfer.TransactionId) ||
                    !string.Equals(transferResponse.Transfer.Message, "successful", StringComparison.OrdinalIgnoreCase) ||
                    transferResponse.Transfer.Amount != amount ||
                    !string.Equals(transferResponse.Transfer.Receiver, receiver, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("La confirmation du transfert MonCash est incomplète.");
                }

                return transferResponse;
            }
            catch (JsonException ex)
            {
                throw new Exception("La réponse de l'API n'est pas au format attendu.");
            }
        }
        else
        {
            // Gérer les erreurs HTTP
            throw new HttpRequestException(
                $"MonCash a refusé le transfert ({(int)response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }
}


    // Vérification du statut d'une transaction préfinancée
    public async Task<TransactionStatusMonCash> CheckTransactionStatusMonCashAsync(string reference)
    {
        var accessToken = await GetAccessTokenAsync();
        var transactionStatus = await PostAsync<TransactionStatusMonCash>(
            $"{_baseUrl}/Api/v1/PrefundedTransactionStatus",
            accessToken,
            new { reference });

        if ((transactionStatus.Status != StatusCodes.Status200OK &&
             transactionStatus.Status != StatusCodes.Status202Accepted) ||
            string.IsNullOrWhiteSpace(transactionStatus.TransStatus))
        {
            throw new InvalidOperationException("Le statut du transfert MonCash est incomplet.");
        }

        return transactionStatus;
    }

    // Consultation du solde préfinancé
    public async Task<PrefundedBalance> GetPrefundedBalanceAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        var prefundedBalance = await GetAsync<PrefundedBalance>(
            $"{_baseUrl}/Api/v1/PrefundedBalance",
            accessToken);

        if (prefundedBalance.Status != StatusCodes.Status200OK ||
            prefundedBalance.Balance == null ||
            !string.Equals(prefundedBalance.Balance.Message, "successful", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Le solde préfinancé MonCash est incomplet.");
        }

        return prefundedBalance;
    }

    // Méthode privée pour obtenir un token d'accès
    private async Task<string> GetAccessTokenAsync()
    {
        if (!_monCashConfig.Enabled)
            throw new InvalidOperationException("MonCash est désactivé.");

        var authUrl = $"{_baseUrl}/Api/oauth/token";

        using (var client = _httpClientFactory.CreateClient("MonCash"))     
        {
            string basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_monCashConfig.ClientId}:{_monCashConfig.ClientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var authData = new StringContent("grant_type=client_credentials&scope=read,write", Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await client.PostAsync(authUrl, authData);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            if (!jsonResponse.TryGetProperty("access_token", out var accessTokenProperty) ||
                string.IsNullOrWhiteSpace(accessTokenProperty.GetString()))
            {
                throw new InvalidOperationException("MonCash n'a pas retourné de jeton d'accès valide.");
            }

            return accessTokenProperty.GetString()!;
        }
    }

    // Méthode générique pour les requêtes POST
    private async Task<T> PostAsync<T>(string url, string accessToken, object payload)
    {
        using (var client = _httpClientFactory.CreateClient("MonCash"))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, MonCashJsonOptions)
                ?? throw new JsonException("La réponse MonCash est vide ou invalide.");
        }
    }

    // Méthode générique pour les requêtes GET
    private async Task<T> GetAsync<T>(string url, string accessToken)
    {
        using (var client = _httpClientFactory.CreateClient("MonCash"))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync(url);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent, MonCashJsonOptions)
                ?? throw new JsonException("La réponse MonCash est vide ou invalide.");
        }
    }

    private static readonly JsonSerializerOptions MonCashJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Modèles de données
    public class MonCashConfig
    {
        public bool Enabled { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Environment { get; set; } = "Sandbox";
        public string ReturnUrl { get; set; }
        public string CallbackUrl { get; set; }
        public bool AllowProduction { get; set; }
        public bool EnablePayouts { get; set; }
    }

  public class PaymentDetails
{
    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("payment")]
    public Payment Payment { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } // Utilisez `long` pour les timestamps Unix

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public class Payment
{
    [JsonPropertyName("reference")]
    public string Reference { get; set; }

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; }

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("payer")]
    public string Payer { get; set; }
}


    public class CustomerStatus
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("customerStatus")]
        public CustomerInfo CustomerStatusDetails { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; }
    }

    public class CustomerInfo
    {
        public string Type { get; set; }
        public string[] Status { get; set; }
    }

    public class TransferResponse
{
    public string Path { get; set; }
    public TransferDetails Transfer { get; set; }
    public long Timestamp { get; set; }
    public int Status { get; set; }
}

public class TransferDetails
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("receiver")]
    public string Receiver { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("desc")]
    public string Desc { get; set; }
}


    public class TransactionStatusMonCash
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("transStatus")]
        public string TransStatus { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    public class PrefundedBalance
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("balance")]
        public BalanceDetails Balance { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    public class BalanceDetails
    {
        public decimal Balance { get; set; }
        public string Message { get; set; }
    }

    
}
