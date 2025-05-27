using Linbik.JwtAuthManager;
using Linbik.Server;
using Microsoft.AspNetCore.Mvc;

namespace AspNet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController(ILogger<TestController> logger) : ControllerBase
{
    [HttpGet]
    [LinbikAuthorize]
    public async Task<ActionResult> Get()
    {
        return Ok("ok");
    }

    //app test
    [HttpGet("test")]
    [LinbikAppAuthorize]
    public async Task<ActionResult> GetApp()
    {
        return Ok("ok app");
    }
}
