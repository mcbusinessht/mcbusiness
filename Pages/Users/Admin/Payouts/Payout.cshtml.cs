using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MCBusiness.Models;

namespace MCBusiness.Pages.Users.Admin.Payouts 
{
   public class PayoutModel : PageModel
{
    private readonly MonCashService _monCashService;

    public PayoutModel(MonCashService monCashService)
    {
        _monCashService = monCashService;
    }

    public string StatusMessage { get; set; }

    public void OnPost(decimal amount, string receiver, string desc, string reference)
    {
        StatusMessage = "Les paiements directs sont désactivés. Utilisez un retrait ou un paiement vendeur validé.";
    }
}
}
