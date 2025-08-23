using Linbik.JwtAuthManager.Extensions;
using Linbik.Server.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace AspNet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController(ILogger<TestController> logger) : ControllerBase
{
    [HttpGet("user")]
    [LinbikAuthorize]
    public async Task<ActionResult> Get()
    {
        return Ok("ok user");
    }

    //app test
    [HttpGet("app")]
    [LinbikAppAuthorize]
    public async Task<ActionResult> GetApp()
    {
        return Ok("ok app");
    }
}
