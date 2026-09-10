using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Configuration;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Configuration;
using Linbik.PasetoAuthManager.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.PasetoAuthManager.Extensions;

/// <summary>
/// Extension methods for adding Linbik PASETO v4.public authentication services.
/// PASETO equivalent of <c>AddLinbikPasetoAuth</c>; uses Ed25519 asymmetric signing.
/// </summary>
public static class LinbikPasetoAuthExtensions
{
    private const string LinbikScheme = Core.LinbikDefaults.ClientScheme;
    private const string AuthTokenCookie = Core.LinbikDefaults.AuthTokenCookie;

    /// <summary>
    /// Add Linbik PASETO authentication services with custom options (builder pattern).
    /// </summary>
    public static ILinbikBuilder AddLinbikPasetoAuth(
        this ILinbikBuilder builder,
        Action<PasetoAuthOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        builder.Services.AddSingleton<IValidateOptions<PasetoAuthOptions>, PasetoAuthOptionsValidator>();
        builder.Services.AddSingleton<ILinbikStartupValidator, PasetoAuthStartupValidator>();

        // Auto-generate Ed25519 key pair in KeylessMode
        AddKeylessModePostConfigure(builder.Services);

        AddLinbikAuthenticationDeferred(builder.Services);

        return builder;
    }

    /// <summary>
    /// Add Linbik PASETO authentication services from configuration (builder pattern).
    /// </summary>
    public static ILinbikBuilder AddLinbikPasetoAuth(
        this ILinbikBuilder builder,
        IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        builder.Services.Configure<PasetoAuthOptions>(configuration);
        builder.Services.AddSingleton<IValidateOptions<PasetoAuthOptions>, PasetoAuthOptionsValidator>();
        builder.Services.AddSingleton<ILinbikStartupValidator, PasetoAuthStartupValidator>();

        AddKeylessModePostConfigure(builder.Services);
        AddLinbikAuthenticationDeferred(builder.Services);

        return builder;
    }

    /// <summary>
    /// Add Linbik PASETO authentication services with default options.
    /// </summary>
    public static ILinbikBuilder AddLinbikPasetoAuth(this ILinbikBuilder builder)
    {
        builder.AddLinbikPasetoAuth(_ => { });
        return builder;
    }

    /// <summary>
    /// Auto-generates a key pair when KeylessMode is active and no keys are provided.
    /// For v4.public: Ed25519 keypair. For v4.local: 32-byte symmetric shared key.
    /// Runs before <see cref="PasetoAuthOptionsValidator"/>.
    /// </summary>
    private static void AddKeylessModePostConfigure(IServiceCollection services)
    {
        services.AddOptions<PasetoAuthOptions>()
            .PostConfigure<IOptions<LinbikOptions>>((pasetoOpts, linbikOpts) =>
            {
                if (!linbikOpts.Value.KeylessMode)
                    return;

                var helper = new PasetoHelperService();

                if (pasetoOpts.Mode == Core.Services.Interfaces.PasetoMode.Local)
                {
                    if (string.IsNullOrEmpty(pasetoOpts.SharedKeyBase64))
                        pasetoOpts.SharedKeyBase64 = helper.GenerateSymmetricKey();
                }
                else
                {
                    if (string.IsNullOrEmpty(pasetoOpts.PrivateKeyBase64)
                        && string.IsNullOrEmpty(pasetoOpts.PublicKeyBase64))
                    {
                        var (priv, pub) = helper.GenerateKeyPair();
                        pasetoOpts.PrivateKeyBase64 = priv;
                        pasetoOpts.PublicKeyBase64 = pub;
                    }
                }
            });
    }

    /// <summary>
    /// Adds the PASETO bearer authentication scheme with deferred option resolution.
    /// Token is read from the <see cref="AuthTokenCookie"/> cookie (browser flow).
    /// Mode is forwarded from <see cref="PasetoAuthOptions"/> so the handler picks
    /// the right key (PublicKey for v4.public, SharedKey for v4.local).
    /// </summary>
    private static void AddLinbikAuthenticationDeferred(IServiceCollection services)
    {
        // PASETO-specific local cookie token reader (mode-aware)
        services.TryAddSingleton<ILocalPasetoTokenReader, LocalPasetoTokenReader>();

        services.TryAddSingleton<ILinbikRefreshTokenStore, InMemoryLinbikRefreshTokenStore>();
        services.TryAddScoped<LinbikRefreshTokenManager>();
        services.AddAuthentication();

        services.AddOptions<PasetoBearerOptions>(LinbikScheme)
            .Configure<IOptions<PasetoAuthOptions>>((bearerOptions, pasetoAccessor) =>
            {
                var opts = pasetoAccessor.Value;

                bearerOptions.Mode = opts.Mode;
                bearerOptions.ExpectedAudience = opts.Audience;
                bearerOptions.ExpectedIssuer = opts.Issuer;
                bearerOptions.RequireApplicationToken = false; // user/delegated tokens only

                if (opts.Mode == Core.Services.Interfaces.PasetoMode.Local)
                {
                    if (string.IsNullOrEmpty(opts.SharedKeyBase64))
                        return;
                    bearerOptions.SharedKey = opts.SharedKeyBase64;
                }
                else
                {
                    if (string.IsNullOrEmpty(opts.PublicKeyBase64))
                        return;
                    bearerOptions.PublicKey = opts.PublicKeyBase64;
                }

                // Read PASETO from cookie instead of Authorization header
                bearerOptions.TokenRetriever = request => request.Cookies[AuthTokenCookie];
            });

        services.AddAuthentication()
            .AddLinbikPasetoBearer(LinbikScheme, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("LinbikAuthorize", policy =>
            {
                policy.AddAuthenticationSchemes(LinbikScheme);
                policy.RequireAuthenticatedUser();
            });
        });
    }

    /// <summary>
    /// Startup validator for Linbik.PasetoAuthManager module.
    /// Forces eager validation of <see cref="PasetoAuthOptions"/> and handles AutoUpdateRedirectUri.
    /// </summary>
    private sealed class PasetoAuthStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.PasetoAuthManager";
        public int Order => 10;

        public void Validate(IServiceProvider services)
        {
            var pasetoOptions = services.GetRequiredService<IOptions<PasetoAuthOptions>>();
            _ = pasetoOptions.Value;

            if (pasetoOptions.Value.AutoUpdateRedirectUri)
            {
                ScheduleRedirectUriAutoUpdate(services, pasetoOptions.Value);
            }
        }

        private static void ScheduleRedirectUriAutoUpdate(IServiceProvider services, PasetoAuthOptions pasetoOptions)
        {
            var linbikOptions = services.GetService<IOptions<LinbikOptions>>()?.Value;
            if (linbikOptions is null || string.IsNullOrWhiteSpace(linbikOptions.Name))
                return;

            var client = linbikOptions.Clients?.FirstOrDefault();
            if (client is null || string.IsNullOrWhiteSpace(client.RedirectUrl))
                return;

            var baseUrl = client.RedirectUrl.TrimEnd('/');
            var callbackPath = pasetoOptions.LoginCallbackPath?.TrimStart('/') ?? "api/linbik/callback";
            var redirectUri = $"{baseUrl}/{callbackPath}";
            var name = linbikOptions.Name;

            _ = Task.Run(async () =>
            {
                var loggerFactory = services.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("Linbik.AutoUpdate");

                try
                {
                    using var scope = services.CreateScope();
                    var authClient = scope.ServiceProvider.GetService<ILinbikAuthClient>();
                    if (authClient is null)
                    {
                        logger?.LogWarning("ILinbikAuthClient not available, skipping RedirectUri auto-update.");
                        return;
                    }

                    var success = await authClient.UpdateClientRedirectUriByNameAsync(
                        name, redirectUri, CancellationToken.None);

                    if (success)
                        logger?.LogInformation("Auto-updated RedirectUri for '{Name}' → {RedirectUri}", name, redirectUri);
                    else
                        logger?.LogWarning("Failed to auto-update RedirectUri for '{Name}'.", name);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Unexpected error during RedirectUri auto-update.");
                }
            });
        }
    }
}
