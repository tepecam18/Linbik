using Linbik.Server.Configuration;
using Linbik.Server.Services;
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
    public static IServiceCollection AddLinbikServer(
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
    public static IServiceCollection AddLinbikServer(
        this IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
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

        // Create RSA key as a managed singleton to ensure proper lifecycle
        // The RSA instance is created once and reused for all token validations
        var rsaKey = CreateRsaSecurityKey(options.PublicKey);

        services.AddAuthentication(authOptions =>
        {
            // Don't set default scheme - let controllers choose with [LinbikIntegrationAuthorize]
        })
        .AddJwtBearer(LinbikIntegrationScheme, jwtOptions =>
        {
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateLifetime = true,
                ValidateIssuer = options.ValidateIssuer,
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidAudience = options.PackageName.ToString(),
                ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes)
            };
        });

        services.AddAuthorization();
    }

    /// <summary>
    /// Creates an RsaSecurityKey from a Base64-encoded public key.
    /// The RSA instance is NOT disposed because it must remain valid for the application lifetime.
    /// </summary>
    /// <param name="publicKeyBase64">Base64-encoded RSA public key in SubjectPublicKeyInfo format.</param>
    /// <returns>An <see cref="RsaSecurityKey"/> for JWT validation.</returns>
    /// <exception cref="ArgumentException">Thrown when the public key is invalid.</exception>
    private static RsaSecurityKey CreateRsaSecurityKey(string publicKeyBase64)
    {
        try
        {
            var rsa = RSA.Create();
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            // Note: We intentionally don't dispose the RSA instance here
            // because RsaSecurityKey takes ownership and the key must remain
            // valid for the entire application lifetime for JWT validation.
            return new RsaSecurityKey(rsa);
        }
        catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
        {
            throw new ArgumentException(
                "Invalid RSA public key format. Ensure the key is Base64-encoded in SubjectPublicKeyInfo format.",
                nameof(publicKeyBase64),
                ex);
        }
    }

    /// <summary>
    /// Use integration authentication middleware
    /// Validates JWT tokens from Linbik.App using RSA public keys
    /// </summary>
    public static IApplicationBuilder UseLinbikServer(this IApplicationBuilder app)
    {
        var validator = app.ApplicationServices.GetService<IntegrationTokenValidator>();

        if (validator == null)
        {
            throw new InvalidOperationException(
                "IntegrationTokenValidator is not registered. " +
                "Please call services.AddLinbikServer() in Program.cs"
            );
        }

        return app;
        //return app.UseMiddleware<IntegrationAuthMiddleware>();
    }

    /// <summary>
    /// Legacy: Use old server endpoints (deprecated)
    /// </summary>
    //[Obsolete("Use AddLinbikServer() and UseLinbikServer() instead. Old endpoint-based auth is deprecated.")]
    //public static IApplicationBuilder UseLinbikServer(this IApplicationBuilder app)
    //{
    //    // Keep for backward compatibility but mark as obsolete
    //    return app;
    //}
}
