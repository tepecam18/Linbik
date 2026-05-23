using Microsoft.AspNetCore.Mvc;

namespace Linbik.Downstream.Sample2.Controllers;

/// <summary>
/// İkinci downstream — auth YOK. Sadece /products endpoint'ini sunar.
/// Gateway'in <b>N:N OpenAPI birleştirmesini</b> göstermek için kullanılır:
/// echo-cluster + products-cluster aynı gateway altında birleşir.
/// </summary>
[ApiController]
[Route("[controller]")]
public class ProductsController : ControllerBase
{
    private static readonly string[] _seed =
    {
        "Linbik Core", "Linbik Server", "Linbik YARP", "Linbik CLI"
    };

    /// <summary>Tüm ürünleri döner + X-Linbik-* header'ları.</summary>
    [HttpGet]
    public IActionResult List()
    {
        var linbikHeaders = Request.Headers
            .Where(h => h.Key.StartsWith("X-Linbik-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        return Ok(new
        {
            service = "Downstream.Sample2",
            items   = _seed,
            linbikHeaders
        });
    }

    /// <summary>Tek bir ürünü id ile döner.</summary>
    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        if (id < 0 || id >= _seed.Length)
            return NotFound();

        return Ok(new
        {
            service = "Downstream.Sample2",
            id,
            name    = _seed[id]
        });
    }
}
