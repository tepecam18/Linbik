using Linbik.Server.Configuration;
using Linbik.Server.Models;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Linbik.Server.Middleware;

/// <summary>
/// Middleware to validate JWT tokens for integration services
/// Extracts token from Authorization header and validates with RSA public key from options
/// Sets User context and HttpContext.Items["LinbikClaims"] if validation succeeds
/// </summary>
public class IntegrationAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IntegrationAuthMiddleware>? _logger;

    public IntegrationAuthMiddleware(RequestDelegate next, ILogger<IntegrationAuthMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IntegrationTokenValidator validator,
        IOptions<ServerOptions> options)
    {
        // Skip validation for anonymous endpoints
        var endpoint = context.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;

        if (allowAnonymous)
        {
            await _next(context);
            return;
        }

        // Check if public key is configured
        var serverOptions = options.Value;
        if (string.IsNullOrEmpty(serverOptions.PublicKey))
        {
            _logger?.LogError("No public key configured for JWT validation");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Server configuration error: missing public key");
            return;
        }

        // Validate token
        var claims = validator.ValidateToken(context);
        if (claims == null)
        {
            _logger?.LogDebug("Token validation failed for request {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid or expired token");
            return;
        }

        // Validate ServiceId matches this service (if configured)
        if (serverOptions.ValidateAudience && serverOptions.ServiceId != Guid.Empty)
        {
            if (claims.ServiceId != serverOptions.ServiceId)
            {
                _logger?.LogWarning("Token ServiceId {TokenServiceId} does not match configured ServiceId {ConfiguredServiceId}",
                    claims.ServiceId, serverOptions.ServiceId);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Token not authorized for this service");
                return;
            }
        }

        // Validate UserId is present
        if (claims.UserId == Guid.Empty)
        {
            _logger?.LogWarning("Token missing UserId claim");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Token missing user identity");
            return;
        }

        // Store LinbikClaims in HttpContext.Items for easy access
        context.Items["LinbikClaims"] = claims;

        // Build ClaimsPrincipal from validated claims
        var claimsList = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, claims.UserId.ToString()),
            new(ClaimTypes.Name, claims.UserName),
            new("display_name", claims.DisplayName),
            new("service_id", claims.ServiceId.ToString()),
            new("iss", claims.Issuer)
        };

        if (claims.AuthorizedParty != Guid.Empty)
        {
            claimsList.Add(new Claim("azp", claims.AuthorizedParty.ToString()));
        }

        // Add raw claims
        foreach (var claim in claims.RawClaims)
        {
            if (!claimsList.Any(c => c.Type == claim.Key))
            {
                claimsList.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var identity = new ClaimsIdentity(claimsList, "LinbikBearer");
        context.User = new ClaimsPrincipal(identity);

        _logger?.LogDebug("Token validated for user {UserId} ({UserName})", claims.UserId, claims.UserName);

        // Continue to next middleware
        await _next(context);
    }
}

/// <summary>
/// Extension methods for IntegrationAuthMiddleware
/// </summary>
public static class IntegrationAuthMiddlewareExtensions
{
    /// <summary>
    /// Use integration authentication middleware for JWT validation
    /// </summary>
    public static IApplicationBuilder UseIntegrationAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<IntegrationAuthMiddleware>();
    }

    /// <summary>
    /// Get LinbikTokenClaims from HttpContext
    /// </summary>
    public static LinbikTokenClaims? GetLinbikClaims(this HttpContext context)
    {
        return context.Items["LinbikClaims"] as LinbikTokenClaims;
    }

    /// <summary>
    /// Get UserId from HttpContext
    /// </summary>
    public static Guid GetLinbikUserId(this HttpContext context)
    {
        return context.GetLinbikClaims()?.UserId ?? Guid.Empty;
    }

    /// <summary>
    /// Get UserName from HttpContext
    /// </summary>
    public static string GetLinbikUserName(this HttpContext context)
    {
        return context.GetLinbikClaims()?.UserName ?? string.Empty;
    }

    /// <summary>
    /// Get DisplayName from HttpContext
    /// </summary>
    public static string GetLinbikDisplayName(this HttpContext context)
    {
        return context.GetLinbikClaims()?.DisplayName ?? string.Empty;
    }

    /// <summary>
    /// Get ServiceId from HttpContext (the service this token was issued for)
    /// </summary>
    public static Guid GetLinbikServiceId(this HttpContext context)
    {
        return context.GetLinbikClaims()?.ServiceId ?? Guid.Empty;
    }

    /// <summary>
    /// Get AuthorizedParty from HttpContext (the main service that requested the token)
    /// </summary>
    public static Guid GetLinbikAuthorizedParty(this HttpContext context)
    {
        return context.GetLinbikClaims()?.AuthorizedParty ?? Guid.Empty;
    }
}
