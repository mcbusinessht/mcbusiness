using Microsoft.Extensions.Options;
using MCBusiness.Models;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Services
{
    /// <summary>
    /// Service centralisé pour la validation reCAPTCHA
    /// </summary>
    public interface IReCaptchaService
    {
        Task<bool> VerifyAsync(string token);
        Task<ReCaptchaValidationResult> VerifyWithDetailsAsync(string token);
        string GetSiteKey();
    }

    public class ReCaptchaService : IReCaptchaService
    {
        private readonly ReCaptchaSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ReCaptchaService> _logger;

        public ReCaptchaService(
            IOptions<ReCaptchaSettings> settings, 
            HttpClient httpClient, 
            ILogger<ReCaptchaService> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> VerifyAsync(string token)
        {
            if (!_settings.IsEnabled)
            {
                _logger.LogWarning("reCAPTCHA is disabled in configuration");
                return true; // Skip validation if disabled
            }

            var result = await VerifyWithDetailsAsync(token);
            return result.IsSuccess;
        }

        public async Task<ReCaptchaValidationResult> VerifyWithDetailsAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return new ReCaptchaValidationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Token reCAPTCHA manquant"
                };
            }

            if (!_settings.IsEnabled)
            {
                _logger.LogWarning("reCAPTCHA is disabled in configuration");
                return new ReCaptchaValidationResult { IsSuccess = true };
            }

            try
            {
                var requestUri = $"{_settings.VerifyUrl}?secret={_settings.SecretKey}&response={token}";
                var response = await _httpClient.GetStringAsync(requestUri);
                
                var json = JObject.Parse(response);
                var isSuccess = (bool)(json["success"] ?? false);
                var score = (double?)json["score"];
                var errorCodes = json["error-codes"]?.ToObject<string[]>() ?? Array.Empty<string>();

                _logger.LogInformation("reCAPTCHA verification: Success={Success}, Score={Score}", isSuccess, score);

                // For reCAPTCHA v3, also check the score
                if (isSuccess && score.HasValue && score.Value < _settings.MinScore)
                {
                    _logger.LogWarning("reCAPTCHA score {Score} is below minimum threshold {MinScore}", score.Value, _settings.MinScore);
                    return new ReCaptchaValidationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Score de sécurité insuffisant",
                        Score = score.Value,
                        ErrorCodes = errorCodes
                    };
                }

                return new ReCaptchaValidationResult
                {
                    IsSuccess = isSuccess,
                    ErrorMessage = isSuccess ? null : "Validation reCAPTCHA échouée",
                    Score = score,
                    ErrorCodes = errorCodes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reCAPTCHA verification");
                return new ReCaptchaValidationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Erreur lors de la validation reCAPTCHA"
                };
            }
        }

        public string GetSiteKey()
        {
            return _settings.SiteKey;
        }
    }

    /// <summary>
    /// Résultat détaillé de la validation reCAPTCHA
    /// </summary>
    public class ReCaptchaValidationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public double? Score { get; set; }
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Attribut de validation personnalisé pour reCAPTCHA
    /// </summary>
    public class ReCaptchaValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            // Cette validation sera effectuée côté serveur avec le service
            return true;
        }
    }
}
