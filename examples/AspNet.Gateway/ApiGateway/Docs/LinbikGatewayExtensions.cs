using ApiGateway.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Docs;

/// <summary>
/// Linbik Gateway için DI ve endpoint wire-up'ları:
/// <list type="bullet">
/// <item>YARP route + cluster otomatik üretimi (<see cref="LinbikGatewayOptions.Sources"/>).</item>
/// <item>OpenAPI aggregator (60s ETag poll + lazy first-load + ETag TTL).</item>
/// <item>Filtrelenmiş <c>self/delegated/apps</c> dokümanları + Scalar UI'leri.</item>
/// </list>
/// </summary>
public static class LinbikGatewayExtensions
{
    public static IServiceCollection AddLinbikGatewayDocs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LinbikGatewayOptions>()
            .Bind(configuration.GetSection(LinbikGatewayOptions.SectionName))
            .Validate(o => o.RefreshIntervalSeconds >= 0, "RefreshIntervalSeconds negatif olamaz.")
            .Validate(o => o.FetchTimeoutSeconds > 0, "FetchTimeoutSeconds > 0 olmalı.")
            .Validate(o => o.EtagMaxAgeSeconds > 0, "EtagMaxAgeSeconds > 0 olmalı.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DownstreamOpenApiPath), "DownstreamOpenApiPath boş olamaz.")
            .Validate(o => o.Sources.All(s => !string.IsNullOrWhiteSpace(s.ServicePrefix) && !string.IsNullOrWhiteSpace(s.Address)),
                "LinbikGateway:Sources içindeki her giriş için ServicePrefix ve Address zorunludur.")
            .ValidateOnStart();

        services.AddSingleton<OpenApiCache>();
        services.AddHttpClient(nameof(OpenApiAggregatorService));
        services.AddSingleton<OpenApiAggregatorService>();
        services.AddHostedService(sp => sp.GetRequiredService<OpenApiAggregatorService>());

        return services;
    }

    /// <summary>
    /// YARP <c>AddReverseProxy()</c> üzerinde çağrılır. Konfigüre edilmiş source'lardan
    /// route + cluster üretip <see cref="InMemoryConfigProvider"/> ile yükler. Böylece
    /// <c>appsettings.json</c> içinde her servis için 3 route + 1 cluster tekrar tekrar
    /// yazmaya gerek kalmaz.
    /// </summary>
    public static IReverseProxyBuilder LoadFromLinbikGateway(
        this IReverseProxyBuilder proxyBuilder,
        IConfiguration configuration)
    {
        var options = new LinbikGatewayOptions();
        configuration.GetSection(LinbikGatewayOptions.SectionName).Bind(options);

        var (routes, clusters) = LinbikRouteBuilder.Build(options.Sources);
        proxyBuilder.LoadFromMemory(routes.ToList(), clusters.ToList());
        return proxyBuilder;
    }

    /// <summary>
    /// Üç filtrelenmiş OpenAPI JSON endpoint'i:
    /// <list type="bullet">
    /// <item><c>/openapi/self.json</c> — Dev anonim; Prod <c>LinbikAuthorize</c> (cookie).</item>
    /// <item><c>/openapi/delegated.json</c> — Dev anonim; Prod <c>LinbikSelfOrApplicationAuthorize</c> (cookie veya Application bearer).</item>
    /// <item><c>/openapi/apps.json</c> — Dev anonim; Prod <c>LinbikSelfOrApplicationAuthorize</c> (cookie veya Application bearer).</item>
    /// </list>
    /// <c>Docs:RequireAuthInDevelopment=true</c> ile Dev'de de Prod davranışına geçilir.
    /// </summary>
    public static IEndpointRouteBuilder MapLinbikGatewayDocs(this IEndpointRouteBuilder endpoints, IWebHostEnvironment env)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<LinbikGatewayOptions>>().Value;
        var requireAuth = !env.IsDevelopment() || options.Docs.RequireAuthInDevelopment;

        var selfJson = endpoints.MapGet("/openapi/self.json", (HttpContext ctx) =>
            ServeAsync(ctx, FilteredDocumentBuilder.FlowSelf));
        if (requireAuth)
            selfJson.RequireAuthorization(LinbikGatewayAuthExtensions.SelfPolicy);

        var delegatedJson = endpoints.MapGet("/openapi/delegated.json", (HttpContext ctx) =>
            ServeAsync(ctx, FilteredDocumentBuilder.FlowDelegated));
        if (requireAuth)
            delegatedJson.RequireAuthorization(LinbikGatewayAuthExtensions.SelfOrApplicationPolicy);

        var appsJson = endpoints.MapGet("/openapi/apps.json", (HttpContext ctx) =>
            ServeAsync(ctx, FilteredDocumentBuilder.FlowApplication));
        if (requireAuth)
            appsJson.RequireAuthorization(LinbikGatewayAuthExtensions.SelfOrApplicationPolicy);

        return endpoints;
    }

    /// <summary>
    /// Üç Scalar referans sayfası: <c>/docs/self</c>, <c>/docs/delegated</c>, <c>/docs/apps</c>.
    /// <para>
    /// Dev'de hepsi anonim. Prod'da (veya <c>Docs:RequireAuthInDevelopment=true</c>) <b>cookie</b>
    /// (<c>LinbikAuthorize</c>) ile açılır: anonim ziyaretçi <c>/api/linbik/login</c>'e yönlendirilir.
    /// Alttaki JSON endpoint'i ayrıca kendi auth'unu uygular (delegated/apps için bearer);
    /// kullanıcı Scalar'ın "Authentication" panelinden bearer token girer.
    /// </para>
    /// </summary>
    public static IEndpointRouteBuilder MapLinbikScalar(this IEndpointRouteBuilder endpoints, IWebHostEnvironment env)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<LinbikGatewayOptions>>().Value;
        var requireAuth = !env.IsDevelopment() || options.Docs.RequireAuthInDevelopment;

        var selfUi = endpoints.MapScalarApiReference("/docs/self", o =>
        {
            o.WithTitle("Linbik Gateway — Self")
             .WithOpenApiRoutePattern("/openapi/self.json");
        });
        if (requireAuth)
            selfUi.RequireAuthorization(LinbikGatewayAuthExtensions.SelfPolicy);

        var delegatedUi = endpoints.MapScalarApiReference("/docs/delegated", o =>
        {
            o.WithTitle("Linbik Gateway — Delegated")
             .WithOpenApiRoutePattern("/openapi/delegated.json");
        });
        if (requireAuth)
            delegatedUi.RequireAuthorization(LinbikGatewayAuthExtensions.SelfPolicy);

        var appsUi = endpoints.MapScalarApiReference("/docs/apps", o =>
        {
            o.WithTitle("Linbik Gateway — Apps")
             .WithOpenApiRoutePattern("/openapi/apps.json");
        });
        if (requireAuth)
            appsUi.RequireAuthorization(LinbikGatewayAuthExtensions.SelfPolicy);

        return endpoints;
    }

    private static async Task ServeAsync(HttpContext ctx, string flow)
    {
        var aggregator = ctx.RequestServices.GetRequiredService<OpenApiAggregatorService>();
        var cache = ctx.RequestServices.GetRequiredService<OpenApiCache>();
        var options = ctx.RequestServices.GetRequiredService<IOptions<LinbikGatewayOptions>>().Value;
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ApiGateway.Docs.LinbikGatewayDocs");

        // Doc erişim audit'i: kullanıcı kimliği (anonimse "(anon)") + remote IP + flow.
        var userId = ctx.User?.Identity?.IsAuthenticated == true
            ? (ctx.User.Identity!.Name ?? ctx.User.FindFirst("sub")?.Value ?? "(authenticated)")
            : "(anon)";
        logger.LogInformation("Doc erişimi: flow={Flow} user={User} ip={Ip} path={Path}",
            flow, userId, ctx.Connection.RemoteIpAddress, ctx.Request.Path);

        using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        fetchCts.CancelAfter(TimeSpan.FromSeconds(options.FetchTimeoutSeconds + 1));
        try
        {
            await aggregator.EnsureInitialFetchAsync(fetchCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* fail-open */ }

        if (!cache.HasAny)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ctx.Response.Headers["Retry-After"] = "5";
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            var bytes = System.Text.Encoding.UTF8.GetBytes(
                "OpenAPI aggregator henüz hazır değil; lütfen birkaç saniye sonra tekrar deneyin.");
            await ctx.Response.Body.WriteAsync(bytes, ctx.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (!cache.TryGetFiltered(flow, out var json))
        {
            var snapshots = cache.SnapshotAll();
            json = FilteredDocumentBuilder.Build(snapshots, flow);
            cache.SetFiltered(flow, json);
        }

        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        await ctx.Response.Body.WriteAsync(jsonBytes, ctx.RequestAborted).ConfigureAwait(false);
    }
}
