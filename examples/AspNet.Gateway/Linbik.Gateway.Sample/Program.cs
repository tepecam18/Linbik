using Linbik.Gateway.Sample.Gateway;
using Linbik.Gateway.Sample.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ── Authentication & Authorization ────────────────────────────────────────
// 3 PASETO scheme (Self / Delegated / Apps) + 3 policy
builder.Services.AddLinbikGatewayAuthentication(builder.Configuration);

// ── YARP ──────────────────────────────────────────────────────────────────
// Config-driven routing + ClaimToHeaderTransform (sanitize + inject)
var gatewayName = builder.Configuration["LinbikGateway:Name"] ?? "Linbik.Gateway.Sample";

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddLinbikClaimTransform(gatewayName: gatewayName);

// ── OpenAPI Aggregator ─────────────────────────────────────────────────────
builder.Services.AddSingleton<OpenApiAggregator>();
builder.Services.AddHttpClient("OpenApiAggregator", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ── Health Checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// KeylessMode + Production = güvenlik açığı; startup'ta uyar.
if (app.Environment.IsProduction())
{
    var keylessSchemes = new[] { "Self", "Delegated", "Apps" }
        .Where(s => app.Configuration.GetValue<bool>($"LinbikGateway:Auth:{s}:KeylessMode"))
        .ToArray();

    if (keylessSchemes.Length > 0)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Linbik.Gateway.Startup");
        logger.LogCritical(
            "GÜVENLİK UYARISI: Production ortamında KeylessMode AÇIK olan scheme'ler: {Schemes}. " +
            "İmza doğrulaması yapılmayacak — sadece geliştirme ortamı için kullanın.",
            string.Join(", ", keylessSchemes));
    }
}

app.UseAuthentication();
app.UseAuthorization();

// /health — docker-compose healthcheck için (auth gerektirmez)
app.MapHealthChecks("/health").AllowAnonymous();

// OpenAPI dokümanları: /openapi/self.json, /openapi/delegated.json, /openapi/apps.json
app.MapLinbikOpenApiAggregator();

// YARP proxy — tüm diğer route'lar buraya düşer
app.MapReverseProxy();

app.Run();

// Test projesinden erişilebilmesi için
public partial class Program { }
