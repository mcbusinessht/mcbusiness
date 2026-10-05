using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Pages.Users.AmbassadorSpace.Withdrawals
{
    public class WithdrawalMethods : PageModel
    {
        private readonly ILogger<WithdrawalMethods> _logger;

        public WithdrawalMethods(ILogger<WithdrawalMethods> logger)
        {
            _logger = logger;
        }

        [BindProperty] public decimal Available { get; set; }
        [BindProperty] public decimal Minimum { get; set; } = 1000m; // seuil par défaut, à déplacer en config

        public void OnGet()
        {
            // Calcul minimal: Available = commissions approuvées et disponibles - déjà payées
            // Hypothèse simple: utiliser le Wallet.Balance si celui-ci représente le disponible
            // Sinon, on peut sommer les commissions Approved/Available dont AvailableAt <= now
            try
            {
                // Tentative simple via Wallet
                // TODO: si Wallet non synchronisé, fallback commissions
            }
            catch { }
        }
    }
}
