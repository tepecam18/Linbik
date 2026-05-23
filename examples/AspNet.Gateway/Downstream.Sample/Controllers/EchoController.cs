using Microsoft.AspNetCore.Mvc;

namespace Linbik.Downstream.Sample.Controllers;

/// <summary>
/// Auth YOK — bu downstream servis sadece gateway'den gelen
/// <c>X-Linbik-*</c> header'larını okur ve döner.
/// Gateway'in claim injection'ının çalıştığını doğrulamak için kullanılır.
/// </summary>
[ApiController]
[Route("[controller]")]
public class EchoController : ControllerBase
{
    /// <summary>
    /// Gelen X-Linbik-* header'larını olduğu gibi geri döner.
    /// </summary>
    [HttpGet]
    [HttpGet("{*path}")]
    public IActionResult Echo()
    {
        var linbikHeaders = Request.Headers
            .Where(h => h.Key.StartsWith("X-Linbik-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        return Ok(new
        {
            path        = Request.Path.Value,
            method      = Request.Method,
            linbikHeaders,
            note        = "Bu downstream servis auth kullanmaz. Header'ları yalnızca gateway sağlar."
        });
    }

    /// <summary>
    /// POST echo — body + header'ları döner.
    /// </summary>
    [HttpPost]
    [HttpPost("{*path}")]
    public async Task<IActionResult> EchoPost()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        var linbikHeaders = Request.Headers
            .Where(h => h.Key.StartsWith("X-Linbik-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        return Ok(new
        {
            path         = Request.Path.Value,
            method       = Request.Method,
            linbikHeaders,
            body
        });
    }
}
