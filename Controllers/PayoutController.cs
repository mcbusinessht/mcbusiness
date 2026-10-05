using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MCBusiness.Controllers
{
    [ApiController]
    [Route("api/payout")]
    [Authorize(Roles = "Admin")]
    public class PayoutController : ControllerBase
    {
        private readonly MonCashService _monCashService;

        public PayoutController(MonCashService monCashService)
        {
            _monCashService = monCashService;
        }

        [HttpPost]
        public IActionResult SendPayout([FromBody] PayoutRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            if (request.Amount <= 0)
            {
                return BadRequest("Invalid amount");
            }

            if (string.IsNullOrWhiteSpace(request.Receiver) || string.IsNullOrWhiteSpace(request.Reference))
            {
                return BadRequest("Receiver and reference are required");
            }

            return Conflict(new
            {
                error = "Les paiements directs sont désactivés. Utilisez un retrait ou un paiement vendeur validé afin de garantir les écritures comptables."
            });
        }
    }

    // Modèle de requête
    public class PayoutRequest
    {
        public decimal Amount { get; set; }
        public string Receiver { get; set; }
        public string Desc { get; set; }
        public string Reference { get; set; }
    }

}
