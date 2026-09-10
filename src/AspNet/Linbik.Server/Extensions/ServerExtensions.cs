using Linbik.Core;
using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Interfaces;
using Linbik.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for configuring Linbik Server (integration service side).
/// Provides PASETO v4.public validation with Ed25519 keys for both user-service and application authentication.
/// </summary>
public static class ServerExtensions
{
    /// <summary>
    /// Add Linbik Server services with custom options (builder pattern).
    /// Registers PASETO authentication schemes and token validator.
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
    /// Registers PASETO authentication schemes and token validator.
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

        // Optional DI: kullanıcı kendi ILinbikIntegrationHandler'ını kayıt etmediyse,
        // default LinbikIntegrationHandler (yalnızca log yazar, başarı döner) devreye girer.
        // Override için: services.AddLinbikIntegrationHandler<MyHandler>();
        // TryAdd kullanıldığı için kullanıcının daha önce kaydettiği handler ezilmez.
        services.TryAddScoped<ILinbikIntegrationHandler, LinbikIntegrationHandler>();

        // Default no-op security event sink. Tüketici uygulama kendi adapter'ını (ör. ServiceEventLog'a
        // yazan) kayıt edebilir; TryAdd sayesinde önceden kayıtlı sink ezilmez.
        services.TryAddSingleton<ILinbikSecurityEventSink, NoOpSecurityEventSink>();

        // Add JWT authentication schemes if public key is configured
        if (!string.IsNullOrEmpty(options.PublicKey))
        {
            AddLinbikAuthentication(services, options);
        }
        else
        {
            var logger = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>()
                .CreateLogger("Linbik.Server");
            logger.LogWarning("Linbik.Server: PublicKey is not configured. Delegated and application token validation will be disabled. Please configure PublicKey in ServerOptions.");
        }
    }

    /// <summary>
    /// Linbik kimlik doğrulama şemalarını PASETO v4.public ile kaydeder.
    /// Her iki şema da Ed25519 ile imzalı token'ları doğrular; çapraz enjeksiyon koruması
    /// <see cref="PasetoBearerHandler"/> içinde (per-scheme <c>RequireApplicationToken</c> bayrağı ile) sağlanır.
    /// </summary>
    private static void AddLinbikAuthentication(IServiceCollection services, ServerOptions options)
    {
        services.AddAuthentication()
            // Kullanıcı istekleri için (sub, name, preferred_username, azp) — application token reddeder
            .AddLinbikPasetoBearer(LinbikDefaults.DelegatedScheme, opts =>
            {
                opts.PublicKey = options.PublicKey;
                opts.ExpectedAudience = options.PackageName;
                opts.ExpectedIssuer = options.JwtIssuer;
                opts.RequireApplicationToken = false;
            })
            // Servis-servis istekleri için (token_type=apps) — kullanıcı tokenını reddeder
            .AddLinbikPasetoBearer(LinbikDefaults.ApplicationScheme, opts =>
            {
                opts.PublicKey = options.PublicKey;
                opts.ExpectedAudience = options.PackageName;
                opts.ExpectedIssuer = options.JwtIssuer;
                opts.RequireApplicationToken = true;
            });

        services.AddAuthorization();
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
