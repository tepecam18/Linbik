using Linbik.Core.Interfaces;
using Linbik.Server.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Linbik.Server.Services;

/// <summary>
/// JWT validation service for integration services (payment, survey, comments, etc.)
/// Validates tokens issued by Linbik.App using RSA public keys
/// Does NOT generate tokens - only validates
/// </summary>
public class IntegrationTokenValidator
{
    private readonly IJwtHelper _jwtHelper;
    private readonly ServerOptions _options;

    public IntegrationTokenValidator(IJwtHelper jwtHelper, IOptions<ServerOptions> options)
    {
        _jwtHelper = jwtHelper;
        _options = options.Value;
    }

    /// <summary>
    /// Validate JWT token from Authorization header
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="publicKey">RSA public key (PEM format) from service registration</param>
    /// <returns>User claims if valid, null if invalid</returns>
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(HttpContext context, string publicKey)
    {
        // Extract token from Authorization header
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        // Validate token with public key
        var isValid = await _jwtHelper.ValidateTokenAsync(
            token,
            publicKey,
            expectedAudience: context.Request.Host.ToString(), // Integration service's own ID
            expectedIssuer: _options.JwtIssuer
        );

        if (!isValid)
        {
            return null;
        }

        // Extract claims
        var claims = _jwtHelper.GetTokenClaims(token);
        if (claims == null || !claims.Any())
        {
            return null;
        }

        // Convert to ClaimsPrincipal
        var claimsList = claims.Select(kvp => new Claim(kvp.Key, kvp.Value)).ToList();
        var identity = new ClaimsIdentity(claimsList, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        return principal;
    }

    /// <summary>
    /// Get user ID from validated token claims
    /// </summary>
    public static string? GetUserId(ClaimsPrincipal? principal)
    {
        return principal?.FindFirst("userId")?.Value;
    }

    /// <summary>
    /// Get user name from validated token claims
    /// </summary>
    public static string? GetUserName(ClaimsPrincipal? principal)
    {
        return principal?.FindFirst("userName")?.Value;
    }

    /// <summary>
    /// Get nick name from validated token claims
    /// </summary>
    public static string? GetNickName(ClaimsPrincipal? principal)
    {
        return principal?.FindFirst("nickName")?.Value;
    }
}
