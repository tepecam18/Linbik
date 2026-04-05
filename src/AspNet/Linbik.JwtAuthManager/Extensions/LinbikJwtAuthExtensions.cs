using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Configuration;
using Linbik.Core.Services.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Linbik.JwtAuthManager.Extensions;

// Startup validator is defined as a nested class at the bottom of this file

/// <summary>
/// Extension methods for adding Linbik JWT authentication services
/// </summary>
public static class LinbikJwtAuthExtensions
{
    private const string LinbikScheme = Core.LinbikDefaults.ClientScheme;
    private const string AuthTokenCookie = Core.LinbikDefaults.AuthTokenCookie;

    /// <summary>
    /// Add Linbik JWT authentication services with custom options (builder pattern)
    /// </summary>
    public static ILinbikBuilder AddLinbikJwtAuth(
        this ILinbikBuilder builder,
        Action<JwtAuthOptions> configureOptions)
    {
        var options = new JwtAuthOptions();
        configureOptions(options);
        builder.Services.Configure(configureOptions);
        builder.Services.AddSingleton<IJwtHelper, JwtHelperService>();
        builder.Services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();
        builder.Services.AddSingleton<ILinbikStartupValidator, JwtAuthStartupValidator>();

        // Auto-generate SecretKey in KeylessMode
        AddKeylessModePostConfigure(builder.Services);

        AddLinbikAuthenticationDeferred(builder.Services);

        return builder;
    }

    /// <summary>
    /// Add Linbik JWT authentication services from configuration (builder pattern)
    /// </summary>
    public static ILinbikBuilder AddLinbikJwtAuth(
        this ILinbikBuilder builder,
        IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        builder.Services.Configure<JwtAuthOptions>(configuration);
        builder.Services.AddSingleton<IJwtHelper, JwtHelperService>();
        builder.Services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();
        builder.Services.AddSingleton<ILinbikStartupValidator, JwtAuthStartupValidator>();

        // Auto-generate SecretKey in KeylessMode
        AddKeylessModePostConfigure(builder.Services);

        AddLinbikAuthenticationDeferred(builder.Services);

        return builder;
    }

    public static ILinbikBuilder AddLinbikJwtAuth(this ILinbikBuilder builder)
    {
        builder.AddLinbikJwtAuth(_ => { });
        return builder;
    }

    /// <summary>
    /// Registers a PostConfigure that auto-generates a SecretKey when KeylessMode is active
    /// and no SecretKey is provided. This runs before validation.
    /// </summary>
    private static void AddKeylessModePostConfigure(IServiceCollection services)
    {
        services.AddOptions<JwtAuthOptions>()
            .PostConfigure<IOptions<LinbikOptions>>((jwtOpts, linbikOpts) =>
            {
                if (linbikOpts.Value.KeylessMode && string.IsNullOrEmpty(jwtOpts.SecretKey))
                {
                    var keyBytes = new byte[64]; // 512 bits
                    RandomNumberGenerator.Fill(keyBytes);
                    jwtOpts.SecretKey = Convert.ToBase64String(keyBytes);
                }
            });
    }

    /// <summary>
    /// Add Linbik authentication scheme with deferred option resolution.
    /// This allows PostConfigure-generated keys (e.g., KeylessMode) to be used.
    /// </summary>
    private static void AddLinbikAuthenticationDeferred(IServiceCollection services)
    {
        services.AddAuthentication();

        services.AddOptions<JwtBearerOptions>(LinbikScheme)
            .Configure<IOptions<JwtAuthOptions>>((jwtBearerOptions, jwtAuthOptionsAccessor) =>
            {
                var opts = jwtAuthOptionsAccessor.Value;
                if (string.IsNullOrEmpty(opts.SecretKey))
                    return;

                // Read JWT from cookie instead of Authorization header
                jwtBearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies[AuthTokenCookie];
                        return Task.CompletedTask;
                    }
                };

                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SecretKey)),
                    ValidateIssuer = !string.IsNullOrEmpty(opts.JwtIssuer),
                    ValidIssuer = opts.JwtIssuer,
                    ValidateAudience = !string.IsNullOrEmpty(opts.JwtAudience),
                    ValidAudience = opts.JwtAudience,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        // Register the JwtBearer scheme
        services.AddAuthentication()
            .AddJwtBearer(LinbikScheme, _ => { });

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
    /// Startup validator for Linbik.JwtAuthManager module.
    /// Forces eager validation of <see cref="JwtAuthOptions"/> and verifies critical service registrations.
    /// Also handles AutoUpdateRedirectUri when enabled.
    /// </summary>
    private sealed class JwtAuthStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.JwtAuthManager";
        public int Order => 10;

        public void Validate(IServiceProvider services)
        {
            // Force eager validation of JwtAuthOptions (triggers JwtAuthOptionsValidator)
            var jwtOptions = services.GetRequiredService<IOptions<JwtAuthOptions>>();
            _ = jwtOptions.Value;

            // Verify IJwtHelper is registered
            _ = services.GetService<IJwtHelper>()
                ?? throw new InvalidOperationException(
                    "IJwtHelper is not registered. Call services.AddLinbikJwtAuth() or builder.AddLinbikJwtAuth() in Program.cs.");

            // Auto-update RedirectUri if enabled
            if (jwtOptions.Value.AutoUpdateRedirectUri)
            {
                ScheduleRedirectUriAutoUpdate(services, jwtOptions.Value);
            }
        }

        private static void ScheduleRedirectUriAutoUpdate(IServiceProvider services, JwtAuthOptions jwtOptions)
        {
            var linbikOptions = services.GetService<IOptions<Core.Configuration.LinbikOptions>>()?.Value;
            if (linbikOptions is null || string.IsNullOrWhiteSpace(linbikOptions.Name))
                return;

            // Build redirect URI from first client's BaseUrl + LoginCallbackPath
            var client = linbikOptions.Clients?.FirstOrDefault();
            if (client is null || string.IsNullOrWhiteSpace(client.RedirectUrl))
                return;

            var baseUrl = client.RedirectUrl.TrimEnd('/');
            var callbackPath = jwtOptions.LoginCallbackPath?.TrimStart('/') ?? "api/linbik/callback";
            var redirectUri = $"{baseUrl}/{callbackPath}";
            var name = linbikOptions.Name;

            // Fire-and-forget — non-blocking, errors logged
            _ = Task.Run(async () =>
            {
                var loggerFactory = services.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("Linbik.AutoUpdate");

                try
                {
                    using var scope = services.CreateScope();
                    var authClient = scope.ServiceProvider
                        .GetService<Core.Services.Interfaces.ILinbikAuthClient>();
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
