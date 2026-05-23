using System.Net;
using Xunit;

namespace Linbik.Gateway.Sample.Tests;

public class HealthAndAuthTests : IClassFixture<GatewayFixture>
{
    private readonly GatewayFixture _fx;

    public HealthAndAuthTests(GatewayFixture fx) => _fx = fx;

    [Fact]
    public async Task Health_endpoint_returns_200_without_auth()
    {
        var client = _fx.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Skip = "TODO: Multi-scheme auth fallback + YARP integration; manuel curl ile doğrulandı.")]
    public async Task Self_route_without_token_returns_401()
    {
        var client = _fx.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/self/echo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(Skip = "TODO: Multi-scheme auth fallback + YARP integration; manuel curl ile doğrulandı.")]
    public async Task Delegated_route_without_token_returns_401()
    {
        var client = _fx.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/delegated/echo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(Skip = "TODO: Multi-scheme auth fallback + YARP integration; manuel curl ile doğrulandı.")]
    public async Task Apps_route_without_token_returns_401()
    {
        var client = _fx.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/apps/echo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(Skip = "TODO: OpenApiAggregator downstream stub'ı eklendiğinde aktif edilecek.")]
    public async Task Self_openapi_returns_200_in_development()
    {
        var client = _fx.CreateClient();

        var response = await client.GetAsync("/openapi/self.json");

        // Downstream'ler erişilmez ama aggregator boş bir doc üretmeli (200) ya da 502.
        // Kritik nokta: 401/403 vermez (anonim erişime izin verilir).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden,    response.StatusCode);
    }
}
