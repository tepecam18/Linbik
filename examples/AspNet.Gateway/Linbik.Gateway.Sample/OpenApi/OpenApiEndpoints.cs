using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using System.Text.Json;

namespace Linbik.Gateway.Sample.OpenApi;

/// <summary>
/// <c>/openapi/{audience}.json</c> endpoint'lerini kayıt eden uzantı.
///
/// <para>Default visibility:</para>
/// <list type="bullet">
///   <item><c>self.json</c>      → üretim ortamında 404 (override: <c>LinbikGateway:OpenApi:Self:ExposeInProduction=true</c>)</item>
///   <item><c>delegated.json</c> → üretim dahil açık</item>
///   <item><c>apps.json</c>      → üretim dahil açık</item>
/// </list>
/// </summary>
public static class OpenApiEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    /// <summary>
    /// OpenAPI aggregator endpoint'lerini ve Scalar UI'ı map eder.
    /// </summary>
    public static IEndpointRouteBuilder MapLinbikOpenApiAggregator(
        this IEndpointRouteBuilder endpoints)
    {
        var env    = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var config = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();

        MapAudience(endpoints, "self",      env, config, "LinbikGateway:OpenApi:Self");
        MapAudience(endpoints, "delegated", env, config, "LinbikGateway:OpenApi:Delegated");
        MapAudience(endpoints, "apps",      env, config, "LinbikGateway:OpenApi:Apps");

        // Scalar UI
        endpoints.MapScalarApiReference(opts =>
        {
            opts.WithTitle("Linbik Gateway");
        });

        return endpoints;
    }

    private static void MapAudience(
        IEndpointRouteBuilder endpoints,
        string audience,
        IHostEnvironment env,
        IConfiguration config,
        string configKey)
    {
        var expose = config.GetValue<bool?>($"{configKey}:ExposeInProduction");
        var defaultExpose = audience != "self"; // Self varsayılan olarak prod'da kapalı
        var shouldExpose  = expose ?? defaultExpose;

        if (env.IsProduction() && !shouldExpose)
        {
            // Prod'da kapalı: 404 döndüren bir route ekle (URL discovery'yi engeller)
            endpoints.MapGet($"/openapi/{audience}.json", () => Results.NotFound());
            return;
        }

        endpoints.MapGet($"/openapi/{audience}.json", async (
            OpenApiAggregator aggregator,
            CancellationToken ct) =>
        {
            var doc = await aggregator.GetDocumentAsync(audience, ct);
            if (doc is null)
                return Results.Problem($"'{audience}' dokümanı oluşturulamadı.", statusCode: 503);

            var json = doc.ToJsonString(JsonOpts);
            return Results.Content(json, "application/json");
        })
        .WithName($"openapi-{audience}")
        .ExcludeFromDescription(); // Gateway kendi dokümanlarını Scalar'a meta olarak göstermesin
    }
}
