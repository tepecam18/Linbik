using Linbik.Core.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Interfaces;
using Linbik.JwtAuthManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Linbik.JwtAuthManager.Extensions;

/// <summary>
/// Extension methods for adding Linbik JWT authentication
/// </summary>
public static class LinbikJwtAuthExtensions
{
    /// <summary>
    /// Add Linbik JWT authentication services
    /// </summary>
    public static IServiceCollection AddLinbikJwtAuth(
        this IServiceCollection services,
        Action<JwtAuthOptions> configureOptions)
    {
        services.Configure(configureOptions);
        
        // Add JWT helper for token validation (no generation)
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        
        return services;
    }

    /// <summary>
    /// Add Linbik JWT authentication services (simple overload)
    /// </summary>
    public static IServiceCollection AddLinbikJwtAuth(this IServiceCollection services)
    {
        // Default configuration - just register JWT helper
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        
        return services;
    }

    /// <summary>
    /// Add Linbik JWT authentication services from configuration
    /// </summary>
    public static IServiceCollection AddLinbikJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtAuthOptions>(configuration.GetSection("Linbik:JwtAuth"));
        
        // Add JWT helper for token validation (no generation)
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        
        return services;
    }

    /// <summary>
    /// Use Linbik authentication middleware
    /// Handles login redirect, OAuth callback, and logout
    /// </summary>
    [Obsolete("Middleware not yet implemented. Use JwtAuthManagerExtensions.UseEndpoints() instead.")]
    public static IApplicationBuilder UseLinbikAuthMiddleware(this IApplicationBuilder app)
    {
        // Ensure session is enabled
        app.UseSession();
        
        // TODO: Add Linbik auth middleware when implemented
        // app.UseMiddleware<LinbikAuthMiddleware>();
        
        return app;
    }

    /// <summary>
    /// Legacy: Use old JWT auth endpoints (deprecated)
    /// </summary>
    [Obsolete("Use UseLinbikAuthMiddleware() instead. Old endpoint-based auth is deprecated.")]
    public static IApplicationBuilder UseJwtAuth(this IApplicationBuilder app)
    {
        // Keep old implementation for backward compatibility
        // But mark as obsolete
        return app;
    }
}
