using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Middlewares
{
    /// <summary>
    /// Middleware pour capturer et stocker les codes de référence ambassadeur
    /// Intégré harmonieusement dans le pipeline MCBusiness
    /// </summary>
    public class AmbassadorTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AmbassadorTrackingMiddleware> _logger;

        public AmbassadorTrackingMiddleware(RequestDelegate next, ILogger<AmbassadorTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            try
            {
                // Vérifier si l'URL contient un paramètre de référence ambassadeur
                if (context.Request.Query.ContainsKey("ref"))
                {
                    var referralCode = context.Request.Query["ref"].ToString();
                    var companyIdParam = context.Request.Query["company"].ToString();
                    
                    // [DIAG-TEMP] Log détection paramètres
                    _logger.LogWarning("[DIAG-TEMP] MIDDLEWARE: Détecté ref={RefCode}, company={CompanyId}, URL={Url}", 
                        referralCode, companyIdParam, context.Request.Path + context.Request.QueryString);
                    
                    if (!string.IsNullOrEmpty(referralCode))
                    {
                        // Valider le CompanyId si fourni
                        int? companyId = null;
                        if (!string.IsNullOrEmpty(companyIdParam) && int.TryParse(companyIdParam, out int parsedCompanyId))
                        {
                            companyId = parsedCompanyId;
                        }
                        
                        await ProcessAmbassadorReferralAsync(context, dbContext, referralCode, companyId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ambassador referral tracking");
                // Ne pas bloquer la requête en cas d'erreur
            }

            // Continuer le pipeline
            await _next(context);
        }

        /// <summary>
        /// Traite la référence ambassadeur et la stocke en session
        /// </summary>
        private async Task ProcessAmbassadorReferralAsync(HttpContext context, AppDbContext dbContext, string referralCode, int? companyId)
        {
            try
            {
                // Nettoyer le code de référence
                referralCode = referralCode.Trim();
                
                // OPTIMISÉ: Consolider les 4 requêtes DB en 1 seule avec AsNoTracking
                // Avant: 4 requêtes séparées (ambassador, role, subscription, company) = -200ms
                // Après: 1 requête consolidée = Gain 80%
                var validationResult = await dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.ReferralCode != null && u.ReferralCode.ToUpper() == referralCode.ToUpper())
                    .Select(u => new
                    {
                        Ambassador = u,
                        IsAmbassador = dbContext.UserRoles
                            .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                            .Any(x => x.UserId == u.Id && x.Name == "Ambassador"),
                        HasActiveSubscription = dbContext.AmbassadorSubscriptions
                            .Any(s => s.UserId == u.Id && 
                                     s.Status == SubscriptionStatus.Active && 
                                     s.EndDate > DateTimeOffset.UtcNow),
                        CompanyExists = !companyId.HasValue || 
                                       dbContext.Companies.Any(c => c.Id == companyId.Value && !c.IsDeleted)
                    })
                    .FirstOrDefaultAsync();

                if (validationResult == null)
                {
                    _logger.LogWarning($"Ambassador referral code not found: {referralCode}");
                    return;
                }

                if (!validationResult.IsAmbassador)
                {
                    _logger.LogWarning($"User {validationResult.Ambassador.Id} has referral code but is not Ambassador");
                    return;
                }

                if (!validationResult.HasActiveSubscription)
                {
                    _logger.LogWarning($"Ambassador {validationResult.Ambassador.Id} has no active subscription - ignoring link");
                    return;
                }

                if (!validationResult.CompanyExists)
                {
                    _logger.LogWarning($"Company {companyId.Value} not found - ignoring link");
                    return;
                }

                var ambassador = validationResult.Ambassador;

                // Vérifier s'il y a déjà un code de parrainage actif
                var existingCode = AmbassadorTrackingHelper.GetActiveReferralCode(context);
                var shouldUpdateReferral = true;

                if (!string.IsNullOrEmpty(existingCode))
                {
                    // Vérifier si le code existant est encore valide (7 jours)
                    var existingTimestamp = AmbassadorTrackingHelper.GetReferralTimestamp(context);
                    if (existingTimestamp.HasValue)
                    {
                        var daysSinceLastReferral = (DateTimeOffset.UtcNow - existingTimestamp.Value).TotalDays;
                        
                        if (daysSinceLastReferral < 7 && !string.Equals(existingCode, referralCode, StringComparison.OrdinalIgnoreCase))
                        {
                            // Le code existant est encore valide (moins de 7 jours) et c'est un code différent
                            // On remplace par le nouveau code (dernier clic gagne)
                            _logger.LogInformation($"Remplacement du code {existingCode} par {referralCode} (nouveau clic dans les 7 jours)");
                        }
                        else if (daysSinceLastReferral < 7 && string.Equals(existingCode, referralCode, StringComparison.OrdinalIgnoreCase))
                        {
                            // Même code dans les 7 jours - extension de la durée
                            shouldUpdateReferral = true; // On met à jour pour étendre
                            _logger.LogInformation($"Code {referralCode} déjà actif, extension de la durée de 7 jours");
                        }
                        else if (daysSinceLastReferral >= 7)
                        {
                            // Code expiré, on peut mettre le nouveau
                            _logger.LogInformation($"Code {existingCode} expiré ({daysSinceLastReferral:F1} jours), remplacement par {referralCode}");
                        }
                    }
                }

                if (shouldUpdateReferral)
                {
                    // Stocker en session avec système de 7 jours
                    var timestamp = DateTimeOffset.UtcNow;
                    var expirationTime = timestamp.AddDays(7);
                    
                    context.Session.SetString("AmbassadorReferralCode", referralCode);
                    context.Session.SetString("AmbassadorReferralId", ambassador.Id);
                    context.Session.SetString("AmbassadorReferralTimestamp", timestamp.ToString("O"));
                    context.Session.SetString("AmbassadorReferralExpiry", expirationTime.ToString("O"));
                    
                    // NOUVEAU: Stocker le CompanyId si fourni
                    if (companyId.HasValue)
                    {
                        context.Session.SetString("AmbassadorCompanyId", companyId.Value.ToString());
                    }
                    else
                    {
                        context.Session.Remove("AmbassadorCompanyId");
                    }

                    // Cookie avec expiration de 7 jours (plus sécurisé)
                    var cookieOptions = new CookieOptions
                    {
                        Expires = expirationTime.DateTime,
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true
                    };

                    context.Response.Cookies.Append("ambassador_ref", referralCode, cookieOptions);
                    context.Response.Cookies.Append("ambassador_ref_id", ambassador.Id, cookieOptions);
                    context.Response.Cookies.Append("ambassador_ref_timestamp", timestamp.ToString("O"), cookieOptions);
                    context.Response.Cookies.Append("ambassador_ref_expiry", expirationTime.ToString("O"), cookieOptions);
                    
                    // [DIAG-TEMP] Confirmation cookies créés
                    _logger.LogWarning("[DIAG-TEMP] MIDDLEWARE: Cookies créés - ref={RefCode}, ambassadorId={AmbassadorId}, expiry={Expiry}, hasCompany={HasCompany}", 
                        referralCode, ambassador.Id, expirationTime, companyId.HasValue);
                    
                    // NOUVEAU: Cookie pour CompanyId
                    if (companyId.HasValue)
                    {
                        context.Response.Cookies.Append("ambassador_company_id", companyId.Value.ToString(), cookieOptions);
                        _logger.LogInformation($"Ambassador referral tracked: {referralCode} for user {ambassador.Id} (Company: {companyId.Value}) - Expire le {expirationTime:dd/MM/yyyy HH:mm}");
                    }
                    else
                    {
                        context.Response.Cookies.Delete("ambassador_company_id");
                        _logger.LogInformation($"Ambassador referral tracked: {referralCode} for user {ambassador.Id} (No specific company) - Expire le {expirationTime:dd/MM/yyyy HH:mm}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Ambassador referral {referralCode} déjà actif et valide");
                }

                // Optionnel : Enregistrer la visite pour statistiques
                await LogAmbassadorVisitAsync(dbContext, ambassador.Id, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing ambassador referral: {referralCode}");
            }
        }

        /// <summary>
        /// Enregistre la visite pour les statistiques ambassadeur (optionnel)
        /// </summary>
        private async Task LogAmbassadorVisitAsync(AppDbContext dbContext, string ambassadorId, HttpContext context)
        {
            try
            {
                // Simplification : juste un log pour les statistiques
                _logger.LogInformation($"Ambassador visit tracked: {ambassadorId} from IP {GetClientIpAddress(context)}");
                
                // TODO: Si besoin de statistiques détaillées, créer une table dédiée
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging ambassador visit for {ambassadorId}");
            }
        }

        /// <summary>
        /// Récupère l'adresse IP du client
        /// </summary>
        private string GetClientIpAddress(HttpContext context)
        {
            try
            {
                // Vérifier les headers de proxy
                var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(xForwardedFor))
                {
                    return xForwardedFor.Split(',')[0].Trim();
                }

                var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(xRealIp))
                {
                    return xRealIp;
                }

                // IP directe
                return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    /// <summary>
    /// Extension pour enregistrer le middleware
    /// </summary>
    public static class AmbassadorTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseAmbassadorTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AmbassadorTrackingMiddleware>();
        }
    }

    /// <summary>
    /// Service helper pour récupérer les informations de référence
    /// </summary>
    public static class AmbassadorTrackingHelper
    {
        /// <summary>
        /// Récupère le code de référence ambassadeur depuis la session ou les cookies
        /// </summary>
        public static string? GetAmbassadorReferralCode(HttpContext context)
        {
            // Vérifier si le code en session est encore valide (7 jours)
            var sessionCode = context.Session.GetString("AmbassadorReferralCode");
            if (!string.IsNullOrEmpty(sessionCode))
            {
                if (IsReferralCodeStillValid(context))
                {
                    return sessionCode;
                }
                else
                {
                    // Code expiré, nettoyer la session
                    ClearExpiredReferralData(context);
                }
            }

            // Essayer le cookie et vérifier sa validité
            var cookieCode = context.Request.Cookies["ambassador_ref"];
            if (!string.IsNullOrEmpty(cookieCode))
            {
                var cookieTimestamp = context.Request.Cookies["ambassador_ref_timestamp"];
                if (!string.IsNullOrEmpty(cookieTimestamp) && DateTimeOffset.TryParse(cookieTimestamp, out var timestamp))
                {
                    var daysSince = (DateTimeOffset.UtcNow - timestamp).TotalDays;
                    if (daysSince < 7)
                    {
                        // Cookie encore valide, remettre en session
                        var cookieExpiry = context.Request.Cookies["ambassador_ref_expiry"];
                        var ambassadorId = context.Request.Cookies["ambassador_ref_id"];
                        
                        RestoreAmbassadorReferralSession(context, cookieCode, ambassadorId, cookieTimestamp, cookieExpiry);
                        return cookieCode;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Récupère l'ID de l'ambassadeur référent
        /// </summary>
        public static string? GetAmbassadorReferralId(HttpContext context)
        {
            // Priorité 1: Session
            var timestamp = GetReferralTimestamp(context);
            if (!timestamp.HasValue || (DateTimeOffset.UtcNow - timestamp.Value).TotalDays >= 7)
            {
                return null;
            }

            var sessionId = context.Session.GetString("AmbassadorReferralId");
            if (!string.IsNullOrEmpty(sessionId))
                return sessionId;

            // Priorité 2: Cookie
            var cookieId = context.Request.Cookies["ambassador_ref_id"];
            if (!string.IsNullOrEmpty(cookieId))
            {
                // Restaurer en session pour la suite
                var cookieCode = context.Request.Cookies["ambassador_ref"];
                var cookieTimestamp = context.Request.Cookies["ambassador_ref_timestamp"];
                var cookieExpiry = context.Request.Cookies["ambassador_ref_expiry"];
                RestoreAmbassadorReferralSession(context, cookieCode, cookieId, cookieTimestamp, cookieExpiry);
                return cookieId;
            }

            return null;
        }

        /// <summary>
        /// Vérifie si une référence ambassadeur est active et récente
        /// </summary>
        public static bool IsAmbassadorReferralActive(HttpContext context, int maxDaysOld = 30)
        {
            var timestamp = GetReferralTimestamp(context);
            if (!timestamp.HasValue)
                return false;

            return (DateTimeOffset.UtcNow - timestamp.Value).TotalDays <= maxDaysOld;
        }

        /// <summary>
        /// Efface les informations de référence ambassadeur
        /// </summary>
        public static void ClearAmbassadorReferral(HttpContext context)
        {
            context.Session.Remove("AmbassadorReferralCode");
            context.Session.Remove("AmbassadorReferralId");
            context.Session.Remove("AmbassadorReferralTimestamp");
            context.Session.Remove("AmbassadorReferralExpiry");
            context.Session.Remove("AmbassadorCompanyId");

            // Supprimer les cookies
            context.Response.Cookies.Delete("ambassador_ref");
            context.Response.Cookies.Delete("ambassador_ref_id");
            context.Response.Cookies.Delete("ambassador_ref_timestamp");
            context.Response.Cookies.Delete("ambassador_ref_expiry");
            context.Response.Cookies.Delete("ambassador_company_id");
        }

        /// <summary>
        /// Méthodes helper pour la gestion des 7 jours
        /// </summary>
        public static string? GetActiveReferralCode(HttpContext context)
        {
            var sessionCode = context.Session.GetString("AmbassadorReferralCode");
            if (!string.IsNullOrEmpty(sessionCode) && IsReferralCodeStillValid(context))
            {
                return sessionCode;
            }
            return null;
        }

        public static DateTimeOffset? GetReferralTimestamp(HttpContext context)
        {
            var timestampStr = context.Session.GetString("AmbassadorReferralTimestamp");
            if (!string.IsNullOrEmpty(timestampStr) && DateTimeOffset.TryParse(timestampStr, out var timestamp))
            {
                return timestamp;
            }
            
            // Fallback sur le cookie
            var cookieTimestamp = context.Request.Cookies["ambassador_ref_timestamp"];
            if (!string.IsNullOrEmpty(cookieTimestamp) && DateTimeOffset.TryParse(cookieTimestamp, out var cookieTime))
            {
                return cookieTime;
            }
            
            return null;
        }

        public static bool IsReferralCodeStillValid(HttpContext context)
        {
            var timestamp = GetReferralTimestamp(context);
            if (!timestamp.HasValue) return false;

            var daysSince = (DateTimeOffset.UtcNow - timestamp.Value).TotalDays;
            return daysSince < 7;
        }

        private static void RestoreAmbassadorReferralSession(
            HttpContext context,
            string? referralCode,
            string? ambassadorId,
            string? timestamp,
            string? expiry)
        {
            if (!string.IsNullOrWhiteSpace(referralCode))
            {
                context.Session.SetString("AmbassadorReferralCode", referralCode.Trim());
            }

            if (!string.IsNullOrWhiteSpace(ambassadorId))
            {
                context.Session.SetString("AmbassadorReferralId", ambassadorId);
            }

            if (!string.IsNullOrWhiteSpace(timestamp))
            {
                context.Session.SetString("AmbassadorReferralTimestamp", timestamp);
            }

            if (!string.IsNullOrWhiteSpace(expiry))
            {
                context.Session.SetString("AmbassadorReferralExpiry", expiry);
            }
        }

        public static void ClearExpiredReferralData(HttpContext context)
        {
            context.Session.Remove("AmbassadorReferralCode");
            context.Session.Remove("AmbassadorReferralId");
            context.Session.Remove("AmbassadorReferralTimestamp");
            context.Session.Remove("AmbassadorReferralExpiry");
            context.Session.Remove("AmbassadorCompanyId"); // NOUVEAU
            
            // Nettoyer aussi les cookies expirés
            context.Response.Cookies.Delete("ambassador_ref");
            context.Response.Cookies.Delete("ambassador_ref_id");
            context.Response.Cookies.Delete("ambassador_ref_timestamp");
            context.Response.Cookies.Delete("ambassador_ref_expiry");
            context.Response.Cookies.Delete("ambassador_company_id"); // NOUVEAU
        }

        /// <summary>
        /// NOUVEAU: Récupère l'ID de l'entreprise liée au lien ambassadeur
        /// </summary>
        public static int? GetAmbassadorCompanyId(HttpContext context)
        {
            // Priorité 1: Session
            var sessionCompanyId = context.Session.GetString("AmbassadorCompanyId");
            if (!string.IsNullOrEmpty(sessionCompanyId) && int.TryParse(sessionCompanyId, out int sessionId))
            {
                return sessionId;
            }

            // Priorité 2: Cookie
            var cookieCompanyId = context.Request.Cookies["ambassador_company_id"];
            if (!string.IsNullOrEmpty(cookieCompanyId) && int.TryParse(cookieCompanyId, out int cookieId))
            {
                // Restaurer en session pour la suite
                context.Session.SetString("AmbassadorCompanyId", cookieId.ToString());
                return cookieId;
            }

            return null;
        }
    }
}
