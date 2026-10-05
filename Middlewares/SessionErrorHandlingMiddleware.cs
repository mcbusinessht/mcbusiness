using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace MCBusiness.Middlewares
{
    /// <summary>
    /// Middleware pour gérer les erreurs de session corrompue
    /// </summary>
    public class SessionErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionErrorHandlingMiddleware> _logger;

        public SessionErrorHandlingMiddleware(RequestDelegate next, ILogger<SessionErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (CryptographicException ex) when (ex.Message.Contains("The payload was invalid"))
            {
                // Log l'erreur sans exposer les détails
                _logger.LogWarning("Session cookie corrompue détectée pour IP {IP}. Nettoyage automatique effectué.", 
                    context.Connection.RemoteIpAddress);

                // Nettoyer tous les cookies de session
                ClearSessionCookies(context);

                // Rediriger vers la page d'accueil ou recharger
                if (!context.Response.HasStarted)
                {
                    context.Response.Redirect(context.Request.Path);
                    return;
                }
            }
        }

        private void ClearSessionCookies(HttpContext context)
        {
            try
            {
                // Supprimer les cookies de session
                var cookiesToClear = new[] 
                {
                    "MCBusiness.Session",
                    ".AspNetCore.Session",
                    ".AspNetCore.Identity.Application"
                };

                foreach (var cookieName in cookiesToClear)
                {
                    if (context.Request.Cookies.ContainsKey(cookieName))
                    {
                        context.Response.Cookies.Delete(cookieName, new CookieOptions
                        {
                            Path = "/",
                            SameSite = SameSiteMode.Lax,
                            Secure = context.Request.IsHttps
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du nettoyage des cookies de session");
            }
        }
    }
}
