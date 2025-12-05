using Linbik.Core.Interfaces;
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

/// <summary>
/// Extension methods for Linbik YARP API Gateway
/// </summary>
public static class LinbikYarpExtensions
{
    private const string IntegrationTokenCookiePrefix = "integration_";

    /// <summary>
    /// Add Linbik YARP services for API gateway with token management
    /// </summary>
    public static IServiceCollection AddLinbikYarp(
        this IServiceCollection services,
        Action<YARPOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Add token provider
        services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();

        // Add HTTP client factory
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Add Linbik YARP services from configuration
    /// </summary>
    public static IServiceCollection AddLinbikYarp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<YARPOptions>(configuration.GetSection("Linbik:YARP"));

        // Add token provider
        services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();

        // Add HTTP client factory
        services.AddHttpClient();

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
    /// Map integration service proxy endpoints
    /// Pattern: /{packageName}/{**path} -> {serviceBaseUrl}/{path}
    /// Automatically injects JWT token from integration_{packageName} cookie
    /// </summary>
    public static IEndpointRouteBuilder MapLinbikIntegrationProxy(this IEndpointRouteBuilder endpoints)
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
            endpoints.Map($"/{packageName}/{{**path}}", async (HttpContext context) =>
            {
                var path = context.Request.RouteValues["path"]?.ToString() ?? string.Empty;

                // Get JWT token from cookie
                var cookieName = $"{cookiePrefix}{packageName}";
                var token = context.Request.Cookies[cookieName];

                if (string.IsNullOrEmpty(token))
                {
                    logger?.LogWarning("Integration token not found for {PackageName}", packageName);
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "unauthorized",
                        error_description = $"Integration token not found for {packageName}. Please login again."
                    });
                    return;
                }

                // Build target URL
                var targetUrl = serviceConfig.BaseUrl.TrimEnd('/');
                if (!string.IsNullOrEmpty(path))
                {
                    targetUrl = $"{targetUrl}/{path}";
                }

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
}

/// <summary>
/// Marker class for logging
/// </summary>
internal class IntegrationProxyService { }
