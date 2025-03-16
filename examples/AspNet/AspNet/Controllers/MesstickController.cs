using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace asp.net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MesstickController(ILogger<LoginController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get()
    {
        return Ok();
    }
}
