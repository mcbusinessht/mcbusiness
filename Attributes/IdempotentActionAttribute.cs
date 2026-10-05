using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MCBusiness.Attributes
{
    /// <summary>
    /// Prevents fast duplicate write requests for MVC actions and Razor Page handlers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class IdempotentActionAttribute : ActionFilterAttribute, IPageFilter
    {
        private readonly int _timeoutSeconds;
        private readonly string _keyPrefix;

        public IdempotentActionAttribute(int timeoutSeconds = 10, string keyPrefix = "Idempotent")
        {
            _timeoutSeconds = timeoutSeconds;
            _keyPrefix = keyPrefix;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!IsWriteRequest(context.HttpContext.Request.Method))
            {
                base.OnActionExecuting(context);
                return;
            }

            var actionName = $"{context.Controller.GetType().Name}.{context.ActionDescriptor.DisplayName}";
            if (!TryBeginRequest(context, actionName, out var result))
            {
                context.Result = result;
                return;
            }

            base.OnActionExecuting(context);
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            EndRequest(context);
            base.OnActionExecuted(context);
        }

        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
        }

        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            if (!IsWriteRequest(context.HttpContext.Request.Method))
            {
                return;
            }

            var pageName = context.HandlerInstance?.GetType().Name ?? context.ActionDescriptor.DisplayName;
            var handlerName = context.HandlerMethod?.Name ?? context.ActionDescriptor.DisplayName;
            var actionName = $"{pageName}.{handlerName}";

            if (!TryBeginRequest(context, actionName, out var result))
            {
                context.Result = result;
            }
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            EndRequest(context);
        }

        private bool TryBeginRequest(FilterContext context, string actionName, out IActionResult result)
        {
            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var userId = context.HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var cacheKey = $"{_keyPrefix}:{userId}:{actionName}";

            if (cache.TryGetValue(cacheKey, out _))
            {
                result = new ConflictObjectResult(new
                {
                    error = "Cette action est deja en cours de traitement. Veuillez patienter.",
                    code = "DUPLICATE_REQUEST"
                });
                return false;
            }

            cache.Set(cacheKey, true, TimeSpan.FromSeconds(_timeoutSeconds));
            context.HttpContext.Items["IdempotentCacheKey"] = cacheKey;
            result = null;
            return true;
        }

        private void EndRequest(FilterContext context)
        {
            if (context.HttpContext.Items.TryGetValue("IdempotentCacheKey", out var cacheKey) && cacheKey is string key)
            {
                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                cache.Remove(key);
            }
        }

        private static bool IsWriteRequest(string method)
        {
            return string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
