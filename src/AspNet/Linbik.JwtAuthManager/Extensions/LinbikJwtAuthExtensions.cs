using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Services.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
        builder.Services.AddLinbikJwtAuth(configureOptions);
        return builder;
    }

    /// <summary>
    /// Add Linbik JWT authentication services from configuration (builder pattern)
    /// </summary>
    public static ILinbikBuilder AddLinbikJwtAuth(
        this ILinbikBuilder builder,
        IConfigurationSection configuration)
    {
        builder.Services.AddLinbikJwtAuth(configuration);
        return builder;
    }

    /// <summary>
    /// Add Linbik JWT authentication services with custom options
    /// </summary>
    public static IServiceCollection AddLinbikJwtAuth(
        this IServiceCollection services,
        Action<JwtAuthOptions> configureOptions)
    {
        var options = new JwtAuthOptions();
        configureOptions(options);
        services.Configure(configureOptions);
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();
        services.AddSingleton<ILinbikStartupValidator, JwtAuthStartupValidator>();

        AddLinbikAuthentication(services, options);

        return services;
    }

    /// <summary>
    /// Add Linbik JWT authentication services from configuration
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    public static IServiceCollection AddLinbikJwtAuth(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration.Get<JwtAuthOptions>() ?? new JwtAuthOptions();
        services.Configure<JwtAuthOptions>(configuration);
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();
        services.AddSingleton<ILinbikStartupValidator, JwtAuthStartupValidator>();

        AddLinbikAuthentication(services, options);

        return services;
    }

    /// <summary>
    /// Add Linbik authentication scheme that validates JWT from cookies
    /// </summary>
    private static void AddLinbikAuthentication(IServiceCollection services, JwtAuthOptions options)
    {
        if (string.IsNullOrEmpty(options.SecretKey))
            return;

        services.AddAuthentication(authOptions =>
        {
            // Don't set default scheme - let controllers choose with [LinbikAuthorize]
            // This allows multiple auth schemes to coexist
        })
        .AddJwtBearer(LinbikScheme, jwtOptions =>
        {
            // Read JWT from cookie instead of Authorization header
            jwtOptions.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies[AuthTokenCookie];
                    return Task.CompletedTask;
                }
            };

            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)),
                ValidateIssuer = !string.IsNullOrEmpty(options.JwtIssuer),
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = !string.IsNullOrEmpty(options.JwtAudience),
                ValidAudience = options.JwtAudience,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        services.AddAuthorization();
    }

    /// <summary>
    /// Startup validator for Linbik.JwtAuthManager module.
    /// Forces eager validation of <see cref="JwtAuthOptions"/> and verifies critical service registrations.
    /// </summary>
    private sealed class JwtAuthStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.JwtAuthManager";
        public int Order => 10;

        public void Validate(IServiceProvider services)
        {
            // Force eager validation of JwtAuthOptions (triggers JwtAuthOptionsValidator)
            var options = services.GetRequiredService<IOptions<JwtAuthOptions>>();
            _ = options.Value;

            // Verify IJwtHelper is registered
            _ = services.GetService<IJwtHelper>()
                ?? throw new InvalidOperationException(
                    "IJwtHelper is not registered. Call services.AddLinbikJwtAuth() or builder.AddLinbikJwtAuth() in Program.cs.");
        }
    }
}
