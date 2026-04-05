using Linbik.Core;
using Linbik.Core.Builders.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for configuring Linbik Server (integration service side).
/// Provides JWT validation with RSA public keys for both user-service and S2S authentication.
/// </summary>
public static class ServerExtensions
{
    /// <summary>
    /// Add Linbik Server services with custom options (builder pattern).
    /// Registers JWT authentication schemes and token validator.
    /// </summary>
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder, Action<ServerOptions> configureOptions)
    {
        var options = new ServerOptions();
        configureOptions(options);
        builder.Services.Configure(configureOptions);
        AddCommonServerServices(builder.Services, options);
        return builder;
    }

    /// <summary>
    /// Add Linbik Server services from configuration (builder pattern).
    /// Registers JWT authentication schemes and token validator.
    /// </summary>
    /// <param name="builder">The Linbik builder.</param>
    /// <param name="configuration">The application configuration.</param>
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration.Get<ServerOptions>() ?? new ServerOptions();
        builder.Services.Configure<ServerOptions>(configuration);
        AddCommonServerServices(builder.Services, options);
        return builder;
    }

    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder)
    {
        builder.AddLinbikServer(_ => { });
        return builder;
    }

    private static void AddCommonServerServices(IServiceCollection services, ServerOptions options)
    {
        // Add integration token validator
        services.AddSingleton<IntegrationTokenValidator>();
        services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
        services.AddSingleton<ILinbikStartupValidator, ServerStartupValidator>();

        // Add JWT authentication schemes if public key is configured
        if (!string.IsNullOrEmpty(options.PublicKey))
        {
            AddLinbikAuthentication(services, options);
        }
    }

    /// <summary>
    /// Add Linbik authentication schemes for both user-service and S2S scenarios.
    /// Both schemes validate JWT with the same RSA public key but expect different claims.
    /// </summary>
    private static void AddLinbikAuthentication(IServiceCollection services, ServerOptions options)
    {
        // Create RSA key as a managed singleton to ensure proper lifecycle
        var rsaKey = CreateRsaSecurityKey(options.PublicKey);

        services.AddAuthentication(authOptions =>
        {
            // Don't set default scheme - let controllers choose with attributes:
            // [LinbikUserServiceAuthorize] for user-initiated requests
            // [LinbikS2SAuthorize] for service-to-service requests
        })
        // User-Service scheme: expects user claims (sub, name, preferred_username, azp)
        // Rejects S2S tokens (token_type == "s2s") to prevent cross-scheme injection
        .AddJwtBearer(LinbikDefaults.UserServiceScheme, jwtOptions =>
        {
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateLifetime = true,
                ValidateIssuer = options.ValidateIssuer,
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidAudience = options.PackageName,
                ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes)
            };
            jwtOptions.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                    if (tokenType == "s2s")
                    {
                        context.Fail("S2S tokens are not accepted by LinbikUserService scheme. Use [LinbikS2SAuthorize] instead.");
                    }
                    return Task.CompletedTask;
                }
            };
        })
        // S2S scheme: expects service claims only (source_service_id, source_package_name, role)
        // Rejects user-service tokens (missing token_type == "s2s") to prevent cross-scheme injection
        .AddJwtBearer(LinbikDefaults.S2SScheme, jwtOptions =>
        {
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateLifetime = true,
                ValidateIssuer = options.ValidateIssuer,
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidAudience = options.PackageName,
                ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes)
            };
            jwtOptions.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                    if (tokenType != "s2s")
                    {
                        context.Fail("Only S2S tokens (token_type=s2s) are accepted by LinbikS2S scheme. Use [LinbikUserServiceAuthorize] for user tokens.");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();
    }

    /// <summary>
    /// Creates an RsaSecurityKey from a Base64-encoded public key.
    /// The RSA instance is NOT disposed because it must remain valid for the application lifetime.
    /// </summary>
    private static RsaSecurityKey CreateRsaSecurityKey(string publicKeyBase64)
    {
        try
        {
            var rsa = RSA.Create();
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            return new RsaSecurityKey(rsa);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new ArgumentException(
                "Invalid RSA public key format. Ensure the key is Base64-encoded in SubjectPublicKeyInfo format.",
                nameof(publicKeyBase64),
                ex);
        }
    }

    /// <summary>
    /// Startup validator for Linbik.Server module.
    /// Forces eager validation of <see cref="ServerOptions"/> and verifies critical service registrations.
    /// </summary>
    private sealed class ServerStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.Server";
        public int Order => 20;

        public void Validate(IServiceProvider services)
        {
            // Force eager validation of ServerOptions (triggers ServerOptionsValidator)
            var options = services.GetRequiredService<IOptions<ServerOptions>>();
            _ = options.Value;

            // Verify IntegrationTokenValidator is registered
            _ = services.GetService<IntegrationTokenValidator>()
                ?? throw new InvalidOperationException(
                    "IntegrationTokenValidator is not registered. Call services.AddLinbikServer() or builder.AddLinbikServer() in Program.cs.");
        }
    }
}
