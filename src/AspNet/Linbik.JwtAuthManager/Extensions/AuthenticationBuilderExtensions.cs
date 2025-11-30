using Linbik.JwtAuthManager.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Linbik.JwtAuthManager.Extensions;

/// <summary>
/// Extension methods for adding Linbik JWT Bearer authentication scheme
/// </summary>
public static class AuthenticationBuilderExtensions
{
    private const string LinbikScheme = "LinbikScheme";
    private const string AuthTokenCookie = "authToken";

    /// <summary>
    /// Add Linbik JWT Bearer authentication scheme
    /// Validates JWT tokens from cookies using the configured secret key
    /// </summary>
    public static AuthenticationBuilder AddLinbikScheme(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Linbik:JwtAuth").Get<JwtAuthOptions>();

        return builder.AddJwtBearer(LinbikScheme, opt =>
        {
            if (!string.IsNullOrEmpty(options?.SecretKey))
            {
                opt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies[AuthTokenCookie];
                        return Task.CompletedTask;
                    }
                };

                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)),
                    ValidateIssuer = !string.IsNullOrEmpty(options.JwtIssuer),
                    ValidIssuer = options.JwtIssuer,
                    ValidateAudience = !string.IsNullOrEmpty(options.JwtAudience),
                    ValidAudience = options.JwtAudience
                };
            }
        });
    }
}

/// <summary>
/// Authorize attribute that uses the Linbik authentication scheme
/// </summary>
public class LinbikAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikAuthorizeAttribute()
    {
        AuthenticationSchemes = "LinbikScheme";
    }
}
