using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace MCBusiness.Services
{
    public interface ICookieConsentService
    {
        Task<CookieSettings> GetUserCookieSettingsAsync(HttpContext context);
        Task SaveCookieConsentAsync(HttpContext context, CookieSettings settings);
        Task<bool> HasValidConsentAsync(HttpContext context);
        Task InvalidateConsentAsync(HttpContext context);
        bool IsCookieAllowed(HttpContext context, CookieCategory category);
        List<CookieInfo> GetCookieInformation();
        Task CleanupOldConsentsAsync();
    }

    public class CookieConsentService : ICookieConsentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CookieConsentService> _logger;
        private const string CONSENT_COOKIE_NAME = "cookie_consent";
        private const string SETTINGS_COOKIE_NAME = "cookie_settings";
        private const int CONSENT_DURATION_DAYS = 365;
        private const string CURRENT_VERSION = "1.0";

        public CookieConsentService(AppDbContext context, ILogger<CookieConsentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CookieSettings> GetUserCookieSettingsAsync(HttpContext context)
        {
            try
            {
                // Vérifier d'abord les cookies
                if (context.Request.Cookies.TryGetValue(SETTINGS_COOKIE_NAME, out var settingsCookie))
                {
                    var settings = JsonSerializer.Deserialize<CookieSettings>(settingsCookie);
                    if (settings != null && IsConsentValid(settings))
                    {
                        return settings;
                    }
                }

                // Vérifier la base de données pour les utilisateurs connectés
                var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var record = await _context.CookieConsentRecords
                        .Where(r => r.UserId == userId && r.IsActive)
                        .OrderByDescending(r => r.ConsentDate)
                        .FirstOrDefaultAsync();

                    if (record != null && IsConsentValid(record))
                    {
                        return MapToSettings(record);
                    }
                }

                // Retourner les paramètres par défaut
                return new CookieSettings { Essential = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des paramètres cookies");
                return new CookieSettings { Essential = true };
            }
        }

        public async Task SaveCookieConsentAsync(HttpContext context, CookieSettings settings)
        {
            try
            {
                settings.ConsentDate = DateTimeOffset.UtcNow;
                settings.ConsentVersion = CURRENT_VERSION;
                settings.UserAgent = context.Request.Headers.UserAgent.ToString();
                settings.IpAddress = GetClientIpAddress(context);

                // Sauvegarder dans les cookies
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(CONSENT_DURATION_DAYS),
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true
                };

                var settingsJson = JsonSerializer.Serialize(settings);
                context.Response.Cookies.Append(SETTINGS_COOKIE_NAME, settingsJson, cookieOptions);
                context.Response.Cookies.Append(CONSENT_COOKIE_NAME, "true", cookieOptions);

                // Sauvegarder en base pour les utilisateurs connectés
                var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    // Désactiver les anciens consentements
                    var oldRecords = await _context.CookieConsentRecords
                        .Where(r => r.UserId == userId && r.IsActive)
                        .ToListAsync();

                    foreach (var record in oldRecords)
                    {
                        record.IsActive = false;
                        record.UpdatedAt = DateTimeOffset.UtcNow;
                    }

                    // Créer nouveau consentement
                    var newRecord = new CookieConsentRecord
                    {
                        UserId = userId,
                        SessionId = context.Session.Id,
                        Essential = settings.Essential,
                        Functional = settings.Functional,
                        Analytics = settings.Analytics,
                        Marketing = settings.Marketing,
                        Performance = settings.Performance,
                        ConsentDate = settings.ConsentDate,
                        ConsentVersion = settings.ConsentVersion,
                        UserAgent = settings.UserAgent,
                        IpAddress = settings.IpAddress,
                        IsActive = true
                    };

                    _context.CookieConsentRecords.Add(newRecord);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Consentement cookies sauvegardé pour l'utilisateur {userId ?? "anonyme"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la sauvegarde du consentement cookies");
                throw;
            }
        }

        public async Task<bool> HasValidConsentAsync(HttpContext context)
        {
            try
            {
                var settings = await GetUserCookieSettingsAsync(context);
                return IsConsentValid(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification du consentement");
                return false;
            }
        }

        public async Task InvalidateConsentAsync(HttpContext context)
        {
            try
            {
                // Supprimer les cookies
                context.Response.Cookies.Delete(CONSENT_COOKIE_NAME);
                context.Response.Cookies.Delete(SETTINGS_COOKIE_NAME);

                // Désactiver en base
                var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var records = await _context.CookieConsentRecords
                        .Where(r => r.UserId == userId && r.IsActive)
                        .ToListAsync();

                    foreach (var record in records)
                    {
                        record.IsActive = false;
                        record.UpdatedAt = DateTimeOffset.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Consentement invalidé pour l'utilisateur {userId ?? "anonyme"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'invalidation du consentement");
                throw;
            }
        }

        public bool IsCookieAllowed(HttpContext context, CookieCategory category)
        {
            try
            {
                if (category == CookieCategory.Essential)
                    return true;

                if (context.Request.Cookies.TryGetValue(SETTINGS_COOKIE_NAME, out var settingsCookie))
                {
                    var settings = JsonSerializer.Deserialize<CookieSettings>(settingsCookie);
                    if (settings != null && IsConsentValid(settings))
                    {
                        return category switch
                        {
                            CookieCategory.Functional => settings.Functional,
                            CookieCategory.Analytics => settings.Analytics,
                            CookieCategory.Marketing => settings.Marketing,
                            CookieCategory.Performance => settings.Performance,
                            _ => false
                        };
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la vérification de l'autorisation pour la catégorie {category}");
                return false;
            }
        }

        public List<CookieInfo> GetCookieInformation()
        {
            return new List<CookieInfo>
            {
                // Cookies Essentiels
                new CookieInfo
                {
                    Name = ".AspNetCore.Session",
                    Purpose = "Maintient votre session utilisateur",
                    Duration = "Session (jusqu'à la fermeture du navigateur)",
                    Category = CookieCategory.Essential,
                    Provider = "MCBusiness",
                    IsThirdParty = false,
                    DataProcessing = "ID de session uniquement"
                },
                new CookieInfo
                {
                    Name = ".AspNetCore.Identity.Application",
                    Purpose = "Maintient votre connexion",
                    Duration = "14 jours ou déconnexion",
                    Category = CookieCategory.Essential,
                    Provider = "MCBusiness",
                    IsThirdParty = false,
                    DataProcessing = "Token d'authentification"
                },
                new CookieInfo
                {
                    Name = "cookie_consent",
                    Purpose = "Mémorise vos préférences de cookies",
                    Duration = "1 an",
                    Category = CookieCategory.Essential,
                    Provider = "MCBusiness",
                    IsThirdParty = false,
                    DataProcessing = "Préférences de consentement"
                },

                // Cookies Fonctionnels
                new CookieInfo
                {
                    Name = "user_preferences",
                    Purpose = "Mémorise vos préférences d'interface (thème, langue)",
                    Duration = "1 an",
                    Category = CookieCategory.Functional,
                    Provider = "MCBusiness",
                    IsThirdParty = false,
                    DataProcessing = "Préférences utilisateur"
                },
                new CookieInfo
                {
                    Name = "cart_data",
                    Purpose = "Sauvegarde le contenu de votre panier",
                    Duration = "30 jours",
                    Category = CookieCategory.Functional,
                    Provider = "MCBusiness",
                    IsThirdParty = false,
                    DataProcessing = "Données du panier d'achat"
                },

                // Cookies Analytics
                new CookieInfo
                {
                    Name = "_ga",
                    Purpose = "Distingue les utilisateurs pour Google Analytics",
                    Duration = "2 ans",
                    Category = CookieCategory.Analytics,
                    Provider = "Google",
                    IsThirdParty = true,
                    DataProcessing = "Données d'usage anonymisées"
                },
                new CookieInfo
                {
                    Name = "_ga_*",
                    Purpose = "Collecte des données d'utilisation du site",
                    Duration = "2 ans",
                    Category = CookieCategory.Analytics,
                    Provider = "Google",
                    IsThirdParty = true,
                    DataProcessing = "Statistiques de navigation"
                },

                // Cookies Performance
                new CookieInfo
                {
                    Name = "performance_tracking",
                    Purpose = "Mesure les performances du site",
                    Duration = "7 jours",
                    Category = CookieCategory.Performance,
                    Provider = "MCBusiness",
                    IsThirdParty = false,
                    DataProcessing = "Métriques de performance"
                },

                // Cookies Marketing
                new CookieInfo
                {
                    Name = "_fbp",
                    Purpose = "Suivi publicitaire Facebook",
                    Duration = "3 mois",
                    Category = CookieCategory.Marketing,
                    Provider = "Facebook",
                    IsThirdParty = true,
                    DataProcessing = "Données de ciblage publicitaire"
                }
            };
        }

        public async Task CleanupOldConsentsAsync()
        {
            try
            {
                var cutoffDate = DateTimeOffset.UtcNow.AddDays(-CONSENT_DURATION_DAYS * 2);
                var oldRecords = await _context.CookieConsentRecords
                    .Where(r => r.ConsentDate < cutoffDate && !r.IsActive)
                    .ToListAsync();

                if (oldRecords.Any())
                {
                    _context.CookieConsentRecords.RemoveRange(oldRecords);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Nettoyage de {oldRecords.Count} anciens consentements");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du nettoyage des consentements");
            }
        }

        private bool IsConsentValid(CookieSettings settings)
        {
            return settings.ConsentDate.AddDays(CONSENT_DURATION_DAYS) > DateTimeOffset.UtcNow &&
                   settings.ConsentVersion == CURRENT_VERSION;
        }

        private bool IsConsentValid(CookieConsentRecord record)
        {
            return record.ConsentDate.AddDays(CONSENT_DURATION_DAYS) > DateTimeOffset.UtcNow &&
                   record.ConsentVersion == CURRENT_VERSION &&
                   record.IsActive;
        }

        private CookieSettings MapToSettings(CookieConsentRecord record)
        {
            return new CookieSettings
            {
                Essential = record.Essential,
                Functional = record.Functional,
                Analytics = record.Analytics,
                Marketing = record.Marketing,
                Performance = record.Performance,
                ConsentDate = record.ConsentDate,
                ConsentVersion = record.ConsentVersion,
                UserAgent = record.UserAgent,
                IpAddress = record.IpAddress
            };
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Connection.RemoteIpAddress?.ToString();
            }
            return ipAddress ?? "Unknown";
        }
    }
}
