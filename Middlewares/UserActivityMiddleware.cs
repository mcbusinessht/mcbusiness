using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MCBusiness.Models;
using MCBusiness.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using System.Web;


namespace MCBusiness.Middlewares
{
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserActivityMiddleware> _logger;
        private readonly UserActivityBackgroundService _backgroundService;
        private static readonly Regex SqlInjectionRegex = new Regex(@"(select|insert|delete|update|drop|;|--|'|""|\bOR\b|\bAND\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex XssRegex = new Regex(@"<script.*?>.*?</script>|on\w+=\\""[^\""]*\\""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public UserActivityMiddleware(
            RequestDelegate next, 
            ILogger<UserActivityMiddleware> logger,
            UserActivityBackgroundService backgroundService)
        {
            _next = next;
            _logger = logger;
            _backgroundService = backgroundService;
        }


        public async Task Invoke(HttpContext context)
        {
            // REFRESH AUTH COOKIE ON USER ACTIVITY FOR 30-MIN INACTIVITY LOGOUT
            // This enables SlidingExpiration to work properly (re-issue cookie on each request)
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, context.User);
            }

            var userName = context.User.Identity?.Name ?? "Anonymous";
            var path = context.Request.Path.ToString().ToLower();
            var method = context.Request.Method;
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var timestamp = DateTimeOffset.UtcNow;
            int? productId = null;
            string actionType = "General Action";

            // Detect SQL Injection
            foreach (var queryValue in context.Request.Query.SelectMany(q => q.Value))
            {
                if (SqlInjectionRegex.IsMatch(queryValue))
                {
                    _logger.LogWarning($"SQL Injection Attempt detected from {ipAddress} by {userName} on path {path}");
                    actionType = "SQL Injection Attempt";
                }
            }

            // Detect XSS
            foreach (var formValue in context.Request.HasFormContentType ? context.Request.Form.SelectMany(f => f.Value) : Enumerable.Empty<string>())
            {
                if (XssRegex.IsMatch(formValue))
                {
                    _logger.LogWarning($"XSS Attempt detected from {ipAddress} by {userName} on path {path}");
                    actionType = "XSS Attempt";
                }
            }


            // Identify Product Actions
            if (path.StartsWith("/shop/categories/products/details"))
            {
                productId = int.TryParse(context.Request.Query["id"], out int id) ? id : (int?)null;
                actionType = "Product View";
            }
            else if (path.StartsWith("/mycart/") && method == "POST")
            {
                productId = int.TryParse(context.Request.Query["id"], out int id) ? id : (int?)null;
                actionType = "Add To Cart";
            }
            else if (path.StartsWith("/cart/remove/") && method == "DELETE")
            {
                productId = int.TryParse(context.Request.Query["id"], out int id) ? id : (int?)null;
                actionType = "Remove From Cart";
            }
            else if (path.StartsWith("/deposit/payment/") && method == "POST")
            {
                actionType = "Purchase";
            }
            else if (path.StartsWith("/deposit/paymentmethods/") && method == "POST")
            {
                actionType = "Purchase";
            }

            // Log Critical Actions
            if (path.Contains("/users/admin") || path.Contains("/account") || path.Contains("/orders") || path.Contains("/payment"))
            {
                actionType = "Critical Action";
            }

            // Ignorer les requêtes pour les fichiers CSS et l'API timezone
            if (path.EndsWith(".css") || path.Contains("/api/timezone"))
            {
                await _next(context);
                return;
            }


            // OPTIMISÉ: Log All Actions via queue background (non-bloquant)
            // Gain: -100ms par requête HTTP (pas de blocage DB synchrone)
            var activity = new UserActivityLog
            {
                UserName = userName,
                Path = path,
                Method = method,
                IpAddress = ipAddress,
                Timestamp = timestamp,
                ProductId = productId,
                ActionType = actionType,
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Referrer = context.Request.Headers["Referer"].ToString()
            };

            // Enqueue au lieu de SaveChangesAsync() - Non-bloquant !
            _backgroundService.EnqueueActivity(activity);

            await _next(context);
        }

        // Utility method to extract ID from URL
        
        
    }
    
}
