using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MCBusiness.Filters
{
    public class AdminAccessFilter : IPageFilter
    {
        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            var path = context.HttpContext.Request.Path.Value;
            var isSecondAdmin = context.HttpContext.User.IsInRole("SecondAdmin");

            // Vérifie si c'est un sous-répertoire d'Admin (sauf la racine)
            if (isSecondAdmin && path.StartsWith("/Users/Admin/") && path != "/Users/Admin/" && 
            (path.Contains("/Edit") || path.Contains("/Delete")))
            {
                context.Result = new RedirectToPageResult("/AccessDenied");
            }
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context) { }
        public void OnPageHandlerSelected(PageHandlerSelectedContext context) { }
    }
}
