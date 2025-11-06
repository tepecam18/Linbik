using Linbik.Core.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Middleware;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for Linbik.Server integration services
/// </summary>
public static class LinbikServerExtensions
{
    /// <summary>
    /// Add Linbik Server services for integration services (payment, survey, comments, etc.)
    /// Provides JWT validation with RSA public keys - NO token generation
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationAuth(
        this IServiceCollection services,
        Action<ServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        
        // Add token validator (requires IJwtHelper from Linbik.JwtAuthManager)
        services.AddSingleton<IntegrationTokenValidator>();
        
        return services;
    }

    /// <summary>
    /// Add Linbik Server services from configuration
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServerOptions>(configuration.GetSection("Linbik:Server"));
        
        // Add token validator
        services.AddSingleton<IntegrationTokenValidator>();
        
        return services;
    }

    /// <summary>
    /// Use integration authentication middleware
    /// Validates JWT tokens from Linbik.App using RSA public keys
    /// </summary>
    public static IApplicationBuilder UseLinbikIntegrationAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IntegrationAuthMiddleware>();
    }

    /// <summary>
    /// Legacy: Use old server endpoints (deprecated)
    /// </summary>
    [Obsolete("Use AddLinbikIntegrationAuth() and UseLinbikIntegrationAuth() instead. Old endpoint-based auth is deprecated.")]
    public static IApplicationBuilder UseLinbikServer(this IApplicationBuilder app)
    {
        // Keep for backward compatibility but mark as obsolete
        return app;
    }
}
