using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;

[ApiController]
[Route("api/timezone")]
public class TimeZoneController : ControllerBase
{
    [HttpPost]
    public IActionResult SetTimeZone([FromBody] string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return BadRequest("Fuseau horaire invalide");

        HttpContext.Session.SetString("UserTimeZone", timeZoneId);
        return Ok();
    }

    [HttpGet]
    public IActionResult GetTimeZone()
    {
        var timeZone = HttpContext.Session.GetString("UserTimeZone") ?? "UTC";
        return Ok(new { TimeZone = timeZone });
    }
}
