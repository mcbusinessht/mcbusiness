using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MCBusiness.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/session")]
    public class SessionController : ControllerBase
    {
        [HttpPost("keepalive")]
        public IActionResult KeepAlive()
        {
            HttpContext.Session.SetString("LastKeepAliveUtc", DateTimeOffset.UtcNow.ToString("O"));

            return Ok(new
            {
                authenticated = true,
                refreshedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
