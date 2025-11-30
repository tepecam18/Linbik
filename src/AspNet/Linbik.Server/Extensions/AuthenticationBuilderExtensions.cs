using Linbik.Server.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for adding Linbik JWT Bearer authentication for integration services
/// </summary>
public static class AuthenticationBuilderExtensions
{
    private const string LinbikIntegrationScheme = "LinbikIntegration";

    /// <summary>
    /// Add Linbik Integration authentication scheme
    /// Validates JWT tokens using the service's RSA public key
    /// </summary>
    public static AuthenticationBuilder AddLinbikIntegrationScheme(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Linbik:Server").Get<ServerOptions>();

        return builder.AddJwtBearer(LinbikIntegrationScheme, opt =>
        {
            if (!string.IsNullOrEmpty(options?.PublicKey))
            {
                // Import RSA public key
                var rsa = RSA.Create();
                var publicKeyBytes = Convert.FromBase64String(options.PublicKey);
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                opt.TokenValidationParameters = new TokenValidationParameters
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
            }
        });
    }
}

/// <summary>
/// Authorize attribute for Linbik integration service endpoints
/// </summary>
public class LinbikIntegrationAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikIntegrationAuthorizeAttribute()
    {
        AuthenticationSchemes = "LinbikIntegration";
    }
}
