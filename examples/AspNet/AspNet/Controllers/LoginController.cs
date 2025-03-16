using Linbik.JwtAuthManager;
using Microsoft.AspNetCore.Mvc;

namespace asp.net.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoginController(ILogger<LoginController> logger) : ControllerBase
{
    [HttpGet]
    [LinbikScheme]
    public async Task<ActionResult> Get()
    {
        return Ok("ok");
    }
}
