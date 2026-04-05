using Linbik.Core;
using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Services.Interfaces;
using Linbik.YARP.Configuration;
using Linbik.YARP.Interfaces;
using Linbik.YARP.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms;

namespace Linbik.YARP.Extensions;

// Startup validator is defined as a nested class at the bottom of this file

/// <summary>
/// Extension methods for Linbik YARP API Gateway
/// </summary>
public static class LinbikYarpExtensions
{
    private const string IntegrationTokenCookiePrefix = LinbikDefaults.IntegrationTokenPrefix;

    /// <summary>
    /// Add Linbik YARP services for API gateway with token management (builder pattern)
    /// </summary>
    public static ILinbikBuilder AddLinbikYarp(
        this ILinbikBuilder builder,
        Action<YARPOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);

        // Add token provider for user-context tokens
        builder.Services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();

        // Add S2S token provider for service-to-service tokens
        builder.Services.AddSingleton<IS2STokenProvider, S2STokenProvider>();

        // Add validators
        builder.Services.AddSingleton<IValidateOptions<YARPOptions>, YARPOptionsValidator>();
        builder.Services.AddSingleton<ILinbikStartupValidator, YarpStartupValidator>();

        // Add S2S service client with HttpClientFactory
        builder.Services.AddS2SHttpClient();

        return builder;
    }

    /// <summary>
    /// Add Linbik YARP services from configuration (builder pattern)
    /// </summary>
    public static ILinbikBuilder AddLinbikYarp(
        this ILinbikBuilder builder,
        IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        builder.Services.Configure<YARPOptions>(configuration);

        // Add token provider for user-context tokens
        builder.Services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();

        // Add S2S token provider for service-to-service tokens
        builder.Services.AddSingleton<IS2STokenProvider, S2STokenProvider>();

        // Add validators
        builder.Services.AddSingleton<IValidateOptions<YARPOptions>, YARPOptionsValidator>();
        builder.Services.AddSingleton<ILinbikStartupValidator, YarpStartupValidator>();

        // Add S2S service client with HttpClientFactory
        builder.Services.AddS2SHttpClient();
        
        return builder;
    }

    public static ILinbikBuilder AddLinbikYarp(this ILinbikBuilder builder)
    {
        builder.AddLinbikYarp(_ => { });
        return builder;
    }



    /// <summary>
    /// Add S2S HttpClient with resilience configuration
    /// </summary>
    private static IServiceCollection AddS2SHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient<IS2SServiceClient, S2SServiceClient>("LinbikS2SServiceClient")
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetService<IOptions<YARPOptions>>()?.Value;
                client.Timeout = TimeSpan.FromSeconds(options?.S2STimeoutSeconds ?? 30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

        return services;
    }

    /// <summary>
    /// Add Linbik token injection transform to YARP proxy
    /// Automatically injects JWT tokens into proxied requests
    /// </summary>
    public static IReverseProxyBuilder AddLinbikTokenTransform(this IReverseProxyBuilder builder)
    {
        builder.AddTransforms(builderContext =>
        {
            // Add request transform to inject token
            builderContext.AddRequestTransform(async transformContext =>
            {
                var tokenProvider = transformContext.HttpContext.RequestServices
                    .GetRequiredService<ITokenProvider>();

                var authService = transformContext.HttpContext.RequestServices
                    .GetService<IAuthService>();

                // Get service package name from route metadata or header
                var servicePackage = transformContext.HttpContext.Request.Headers["X-Service-Package"].ToString();

                if (string.IsNullOrEmpty(servicePackage))
                {
                    // Try to get from route values
                    if (transformContext.HttpContext.Request.RouteValues.TryGetValue("servicePackage", out var value))
                    {
                        servicePackage = value?.ToString() ?? string.Empty;
                    }
                }

                if (!string.IsNullOrEmpty(servicePackage))
                {
                    try
                    {
                        // Get integration token (will auto-refresh if expired)
                        var token = await tokenProvider.GetIntegrationTokenAsync(servicePackage);

                        if (!string.IsNullOrEmpty(token))
                        {
                            // Inject token into Authorization header
                            transformContext.ProxyRequest.Headers.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }
                        else
                        {
                            // Token refresh failed - redirect to login
                            if (authService != null)
                            {
                                transformContext.HttpContext.Response.StatusCode = 401;
                                await transformContext.HttpContext.Response.WriteAsync(
                                    "Authentication expired. Please log in again.");
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error using ILogger instead of Console.WriteLine
                        var logger = transformContext.HttpContext.RequestServices
                            .GetService<ILogger<MultiJwtTokenProvider>>();
                        logger?.LogError(ex, "Token injection failed for service {ServicePackage}", servicePackage);

                        transformContext.HttpContext.Response.StatusCode = 500;
                        await transformContext.HttpContext.Response.WriteAsync(
                            "Failed to inject authentication token");
                        return;
                    }
                }
            });

            // Add response transform for error handling
            builderContext.AddResponseTransform(async transformContext =>
            {
                // Handle 401 responses from integration services
                if (transformContext.HttpContext.Response.StatusCode == 401)
                {
                    var tokenProvider = transformContext.HttpContext.RequestServices
                        .GetRequiredService<ITokenProvider>();

                    var authService = transformContext.HttpContext.RequestServices
                        .GetService<IAuthService>();

                    // Try to refresh tokens
                    if (authService != null)
                    {
                        var refreshed = await authService.RefreshTokensAsync(transformContext.HttpContext);

                        if (!refreshed)
                        {
                            // Refresh failed - clear cache and redirect to login
                            tokenProvider.ClearCache();

                            transformContext.HttpContext.Response.StatusCode = 401;
                            transformContext.HttpContext.Response.Headers["X-Linbik-Auth-Error"] = "token_expired";
                            await transformContext.HttpContext.Response.WriteAsync(
                                "Session expired. Please log in again.");
                        }
                    }
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Map server proxy routes for Linbik.Server integration endpoints
    /// Pattern: /{sourcePath}/{**path} -> {targetBaseUrl}/{targetPath}/{path}
    /// Automatically injects JWT token from integration_{packageName} cookie
    /// </summary>
    public static IEndpointRouteBuilder UseLinbikYarp(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<YARPOptions>>().Value;
        var httpClientFactory = endpoints.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var logger = endpoints.ServiceProvider.GetService<ILogger<IntegrationProxyService>>();

        foreach (var integration in options.IntegrationServices)
        {
            var packageName = integration.Key;
            var serviceConfig = integration.Value;
            var cookiePrefix = options.IntegrationTokenCookiePrefix;

            // Map route: /{packageName}/{**path}
            endpoints.Map($"{integration.Value.SourcePath}/{{**path}}", async (HttpContext context) =>
            {
                var path = context.Request.RouteValues["path"]?.ToString() ?? string.Empty;

                // Get JWT token from cookie
                var cookieName = $"{cookiePrefix}{packageName}";
                var token = context.Request.Cookies[cookieName];



                // Build target URL
                var targetUrl = $"{serviceConfig.TargetBaseUrl}{serviceConfig.TargetPath}";
                
                if (!string.IsNullOrEmpty(path))
                    targetUrl = $"{targetUrl}/{path}";

                // Preserve query string
                if (context.Request.QueryString.HasValue)
                {
                    targetUrl = $"{targetUrl}{context.Request.QueryString}";
                }

                try
                {
                    var client = httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(serviceConfig.TimeoutSeconds);

                    // Create proxy request
                    var requestMessage = new HttpRequestMessage
                    {
                        Method = new HttpMethod(context.Request.Method),
                        RequestUri = new Uri(targetUrl)
                    };

                    // Add Authorization header with JWT token
                    requestMessage.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    // Copy headers (except Host and Authorization)
                    foreach (var header in context.Request.Headers)
                    {
                        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                            header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                            continue;

                        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }

                    // Copy request body for POST/PUT/PATCH
                    if (context.Request.ContentLength > 0 ||
                        context.Request.Headers.ContainsKey("Transfer-Encoding"))
                    {
                        requestMessage.Content = new StreamContent(context.Request.Body);

                        if (context.Request.ContentType != null)
                        {
                            requestMessage.Content.Headers.ContentType =
                                System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                        }
                    }

                    // Send request
                    var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                    // Copy response status
                    context.Response.StatusCode = (int)response.StatusCode;

                    // Copy response headers
                    foreach (var header in response.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }

                    foreach (var header in response.Content.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }

                    // Remove transfer-encoding if present (handled by Kestrel)
                    context.Response.Headers.Remove("transfer-encoding");

                    // Copy response body
                    await response.Content.CopyToAsync(context.Response.Body);
                }
                catch (HttpRequestException ex)
                {
                    logger?.LogError(ex, "Failed to proxy request to {PackageName}: {TargetUrl}", packageName, targetUrl);
                    context.Response.StatusCode = 502;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "bad_gateway",
                        error_description = $"Failed to connect to {packageName} service"
                    });
                }
                catch (TaskCanceledException)
                {
                    logger?.LogWarning("Request to {PackageName} timed out: {TargetUrl}", packageName, targetUrl);
                    context.Response.StatusCode = 504;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "gateway_timeout",
                        error_description = $"Request to {packageName} service timed out"
                    });
                }
            }).WithTags($"Integration.{packageName}");
        }

        return endpoints;
    }

    /// <summary>
    /// Map S2S (Service-to-Service) proxy routes
    /// Pattern: /s2s/{packageName}/{**path} -> {targetBaseUrl}/{targetPath}/{path}
    /// Automatically injects S2S JWT token from cache (no user context required)
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="routePrefix">Route prefix for S2S endpoints (default: "s2s")</param>
    /// <returns>The endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder UseLinbikS2S(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "s2s")
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<YARPOptions>>().Value;
        var httpClientFactory = endpoints.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var s2sTokenProvider = endpoints.ServiceProvider.GetRequiredService<IS2STokenProvider>();
        var logger = endpoints.ServiceProvider.GetService<ILogger<S2SProxyService>>();

        foreach (var integration in options.IntegrationServices)
        {
            var packageName = integration.Key;
            var serviceConfig = integration.Value;

            // Map S2S route: /{routePrefix}/{packageName}/{**path}
            endpoints.Map($"/{routePrefix}/{packageName}/{{**path}}", async (HttpContext context) =>
            {
                var path = context.Request.RouteValues["path"]?.ToString() ?? string.Empty;

                // Get S2S JWT token from provider (auto-cached, auto-refreshed)
                var integrationDetails = await s2sTokenProvider.GetS2SIntegrationAsync(packageName);

                if (integrationDetails == null)
                {
                    logger?.LogWarning("S2S token not available for {PackageName}", packageName);
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "service_unavailable",
                        error_description = $"S2S authentication not available for {packageName}. Check service configuration."
                    });
                    return;
                }

                // Use ServiceUrl from token response if available, fallback to config
                var baseUrl = !string.IsNullOrEmpty(integrationDetails.ServiceUrl)
                    ? integrationDetails.ServiceUrl
                    : serviceConfig.TargetBaseUrl;

                // Build target URL
                var targetUrl = $"{baseUrl}{serviceConfig.TargetPath}";

                if (!string.IsNullOrEmpty(path))
                    targetUrl = $"{targetUrl}/{path}";

                // Preserve query string
                if (context.Request.QueryString.HasValue)
                {
                    targetUrl = $"{targetUrl}{context.Request.QueryString}";
                }

                try
                {
                    var client = httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(serviceConfig.TimeoutSeconds);

                    // Create proxy request
                    var requestMessage = new HttpRequestMessage
                    {
                        Method = new HttpMethod(context.Request.Method),
                        RequestUri = new Uri(targetUrl)
                    };

                    // Add Authorization header with S2S JWT token
                    requestMessage.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", integrationDetails.Token);

                    // Add S2S indicator header
                    requestMessage.Headers.TryAddWithoutValidation("X-Linbik-S2S", "true");

                    // Copy headers (except Host and Authorization)
                    foreach (var header in context.Request.Headers)
                    {
                        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                            header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                            continue;

                        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }

                    // Copy request body for POST/PUT/PATCH
                    if (context.Request.ContentLength > 0 ||
                        context.Request.Headers.ContainsKey("Transfer-Encoding"))
                    {
                        requestMessage.Content = new StreamContent(context.Request.Body);

                        if (context.Request.ContentType != null)
                        {
                            requestMessage.Content.Headers.ContentType =
                                System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                        }
                    }

                    logger?.LogDebug("S2S request to {PackageName}: {Method} {TargetUrl}",
                        packageName, context.Request.Method, targetUrl);

                    // Send request
                    var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                    // Copy response status
                    context.Response.StatusCode = (int)response.StatusCode;

                    // Copy response headers
                    foreach (var header in response.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }

                    foreach (var header in response.Content.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }

                    // Remove transfer-encoding if present (handled by Kestrel)
                    context.Response.Headers.Remove("transfer-encoding");

                    // Copy response body
                    await response.Content.CopyToAsync(context.Response.Body);
                }
                catch (HttpRequestException ex)
                {
                    logger?.LogError(ex, "S2S proxy failed to {PackageName}: {TargetUrl}", packageName, targetUrl);
                    context.Response.StatusCode = 502;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "bad_gateway",
                        error_description = $"S2S connection failed to {packageName} service"
                    });
                }
                catch (TaskCanceledException)
                {
                    logger?.LogWarning("S2S request to {PackageName} timed out: {TargetUrl}", packageName, targetUrl);
                    context.Response.StatusCode = 504;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "gateway_timeout",
                        error_description = $"S2S request to {packageName} service timed out"
                    });
                }
            }).WithTags($"S2S.{packageName}");
        }

        return endpoints;
    }

    /// <summary>
    /// Startup validator for Linbik.YARP module.
    /// Forces eager validation of <see cref="YARPOptions"/> and verifies critical service registrations.
    /// </summary>
    private sealed class YarpStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.YARP";
        public int Order => 30;

        public void Validate(IServiceProvider services)
        {
            // Force eager validation of YARPOptions (triggers YARPOptionsValidator)
            var options = services.GetRequiredService<IOptions<YARPOptions>>();
            _ = options.Value;

            // Verify ITokenProvider is registered
            _ = services.GetService<ITokenProvider>()
                ?? throw new InvalidOperationException(
                    "ITokenProvider is not registered. Call services.AddLinbikYarp() or builder.AddLinbikYarp() in Program.cs.");
        }
    }
}

/// <summary>
/// Marker class for logging
/// </summary>
internal sealed class IntegrationProxyService;

/// <summary>
/// Marker class for S2S logging
/// </summary>
internal sealed class S2SProxyService;
