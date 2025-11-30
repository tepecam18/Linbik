using Linbik.Core.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Linbik.JwtAuthManager.Extensions;

/// <summary>
/// Extension methods for adding Linbik JWT authentication services
/// </summary>
public static class LinbikJwtAuthExtensions
{
    private const string LinbikScheme = "LinbikScheme";
    private const string AuthTokenCookie = "authToken";

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
        
        AddLinbikAuthentication(services, options);
        
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
        var options = configuration.GetSection("Linbik:JwtAuth").Get<JwtAuthOptions>() ?? new JwtAuthOptions();
        services.Configure<JwtAuthOptions>(configuration.GetSection("Linbik:JwtAuth"));
        services.AddSingleton<IJwtHelper, JwtHelperService>();
        
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
}
