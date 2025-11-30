using Linbik.Core.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.JwtAuthManager.Extensions;

/// <summary>
/// Extension methods for adding Linbik JWT authentication services
/// </summary>
public static class LinbikJwtAuthExtensions
{
    /// <summary>
    /// Add Linbik JWT authentication services with custom options
    /// </summary>
    public static IServiceCollection AddLinbikJwtAuth(
        this IServiceCollection services,
        Action<JwtAuthOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        return services;
    }

    /// <summary>
    /// Add Linbik JWT authentication services with default options
    /// </summary>
    public static IServiceCollection AddLinbikJwtAuth(this IServiceCollection services)
    {
        services.Configure<JwtAuthOptions>(_ => { });
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
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        return services;
    }
}
