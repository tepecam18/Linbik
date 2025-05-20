using Linbik.Server;
using Microsoft.AspNetCore.Mvc;

namespace AspNet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MesstickController(ILogger<LoginController> logger) : ControllerBase
{
    [HttpGet]
    [LinbikAppAuthorize]
    public async Task<ActionResult> Get()
    {
        return Ok();
    }
}
