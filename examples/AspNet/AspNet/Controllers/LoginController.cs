using Linbik.JwtAuthManager;
using Microsoft.AspNetCore.Mvc;

namespace AspNet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoginController(ILogger<LoginController> logger) : ControllerBase
{
    [HttpGet]
    [LinbikAuthorize]
    public async Task<ActionResult> Get()
    {
        return Ok("ok");
    }
}
