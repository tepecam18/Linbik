using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Linbik.Gateway.Sample.Tests;

/// <summary>
/// Gateway için test fixture. KeylessMode=true ile başlar; cluster'lar test ortamında
/// gerçek downstream'e değil 127.0.0.1:65000 (asla yanıt vermez) gibi bir adrese
/// yönlendirilir — biz sadece gateway davranışını test ediyoruz, downstream'i değil.
/// </summary>
public sealed class GatewayFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LinbikGateway:Auth:Self:KeylessMode"]      = "true",
                ["LinbikGateway:Auth:Delegated:KeylessMode"] = "true",
                ["LinbikGateway:Auth:Apps:KeylessMode"]      = "true",

                // Test'te downstream gerçekten çağrılmasın
                ["ReverseProxy:Clusters:echo-cluster:Destinations:d1:Address"]
                    = "http://127.0.0.1:65000/",
                ["ReverseProxy:Clusters:products-cluster:Destinations:d1:Address"]
                    = "http://127.0.0.1:65000/"
            });
        });
    }
}
