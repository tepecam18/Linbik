using Linbik.Core.Interfaces;
using Linbik.YARP.Configuration;
using Linbik.YARP.Interfaces;
using Linbik.YARP.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Transforms;

namespace Linbik.YARP.Extensions;

/// <summary>
/// Extension methods for Linbik YARP API Gateway
/// </summary>
public static class LinbikYarpExtensions
{
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
                        // Log error and return 500
                        Console.WriteLine($"Token injection failed: {ex.Message}");
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
    /// Use Linbik YARP with automatic token management
    /// </summary>
    public static IApplicationBuilder UseLinbikYarp(this IApplicationBuilder app)
    {
        // YARP middleware should be added via MapReverseProxy in endpoint routing
        // This method is for additional setup if needed
        return app;
    }

    /// <summary>
    /// Use Linbik YARP proxy (alias for UseLinbikYarp)
    /// </summary>
    public static IApplicationBuilder UseLinbikYarpProxy(this IApplicationBuilder app)
    {
        return app.UseLinbikYarp();
    }
}
