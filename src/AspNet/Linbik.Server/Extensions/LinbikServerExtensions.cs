using Linbik.Server.Configuration;
using Linbik.Server.Middleware;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for Linbik.Server integration services
/// </summary>
public static class LinbikServerExtensions
{
    private const string LinbikIntegrationScheme = "LinbikIntegration";

    /// <summary>
    /// Add Linbik Server services for integration services (payment, survey, comments, etc.)
    /// Provides JWT validation with RSA public keys - NO token generation
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationAuth(
        this IServiceCollection services,
        Action<ServerOptions> configureOptions)
    {
        var options = new ServerOptions();
        configureOptions(options);
        services.Configure(configureOptions);

        // Add token validator as singleton (RSA key is loaded once)
        services.AddSingleton<IntegrationTokenValidator>();
        
        AddLinbikIntegrationAuthentication(services, options);
        
        return services;
    }

    /// <summary>
    /// Add Linbik Server services from configuration
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Linbik:Server").Get<ServerOptions>() ?? new ServerOptions();
        services.Configure<ServerOptions>(configuration.GetSection("Linbik:Server"));
        
        // Add token validator
        services.AddSingleton<IntegrationTokenValidator>();
        
        AddLinbikIntegrationAuthentication(services, options);
        
        return services;
    }

    /// <summary>
    /// Add Linbik Integration authentication scheme that validates JWT with RSA public key
    /// </summary>
    private static void AddLinbikIntegrationAuthentication(IServiceCollection services, ServerOptions options)
    {
        if (string.IsNullOrEmpty(options.PublicKey))
            return;

        // Import RSA public key
        var rsa = RSA.Create();
        var publicKeyBytes = Convert.FromBase64String(options.PublicKey);
        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

        services.AddAuthentication(authOptions =>
        {
            // Don't set default scheme - let controllers choose with [LinbikIntegrationAuthorize]
        })
        .AddJwtBearer(LinbikIntegrationScheme, jwtOptions =>
        {
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateLifetime = true,
                ValidateIssuer = options.ValidateIssuer,
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidAudience = options.ServiceId.ToString(),
                ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes)
            };
        });

        services.AddAuthorization();
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
