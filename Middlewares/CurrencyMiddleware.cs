using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Middlewares
{
    public class CurrencyMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrencyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            string currencyCode = "HTG"; // Default

            // 1. Check if user is authenticated and has preferred currency
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await dbContext.Users
                        .AsNoTracking()
                        .Include(u => u.PreferredCurrency)
                        .FirstOrDefaultAsync(u => u.Id == userId);

                    if (user?.PreferredCurrency != null)
                    {
                        currencyCode = user.PreferredCurrency.Code;
                    }
                }
            }
            // 2. If not authenticated, check cookie
            else if (context.Request.Cookies.TryGetValue("currency", out var cookieCurrency))
            {
                if (!string.IsNullOrEmpty(cookieCurrency) && 
                    (cookieCurrency == "HTG" || cookieCurrency == "USD" || cookieCurrency == "EUR"))
                {
                    currencyCode = cookieCurrency;
                }
            }

            // 3. Store in HttpContext.Items for access in pages
            context.Items["CurrentCurrency"] = currencyCode;

            await _next(context);
        }
    }
}
