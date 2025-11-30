using Linbik.Server.Configuration;
using Linbik.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Linbik.Server.Services;

/// <summary>
/// JWT validation service for integration services (payment, survey, comments, etc.)
/// Validates tokens issued by Linbik.App using RSA public keys
/// Does NOT generate tokens - only validates
/// </summary>
public class IntegrationTokenValidator
{
    private readonly ServerOptions _options;
    private readonly ILogger<IntegrationTokenValidator>? _logger;
    private RSA? _rsaPublicKey;
    private readonly object _keyLock = new();

    public IntegrationTokenValidator(IOptions<ServerOptions> options, ILogger<IntegrationTokenValidator>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
        InitializePublicKey();
    }

    private void InitializePublicKey()
    {
        if (string.IsNullOrEmpty(_options.PublicKey))
        {
            _logger?.LogWarning("No public key configured for JWT validation");
            return;
        }

        lock (_keyLock)
        {
            try
            {
                _rsaPublicKey = RSA.Create();

                // Try to import as PEM first
                if (_options.PublicKey.Contains("-----BEGIN"))
                {
                    _rsaPublicKey.ImportFromPem(_options.PublicKey);
                }
                else
                {
                    // Try as Base64 DER format
                    var keyBytes = Convert.FromBase64String(_options.PublicKey);
                    _rsaPublicKey.ImportSubjectPublicKeyInfo(keyBytes, out _);
                }

                _logger?.LogInformation("RSA public key initialized successfully for service {ServiceId}", _options.ServiceId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize RSA public key");
                _rsaPublicKey = null;
            }
        }
    }

    /// <summary>
    /// Validate JWT token from Authorization header
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Token claims if valid, null if invalid</returns>
    public LinbikTokenClaims? ValidateToken(HttpContext context)
    {
        // Extract token from Authorization header
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("Missing or invalid Authorization header");
            return null;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        return ValidateToken(token);
    }

    /// <summary>
    /// Validate JWT token string
    /// </summary>
    /// <param name="token">JWT token string</param>
    /// <returns>Token claims if valid, null if invalid</returns>
    public LinbikTokenClaims? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger?.LogDebug("Empty token provided");
            return null;
        }

        if (_rsaPublicKey == null)
        {
            _logger?.LogError("RSA public key not initialized");
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(_rsaPublicKey),
                ValidateIssuer = _options.ValidateIssuer,
                ValidIssuer = _options.JwtIssuer,
                ValidateAudience = _options.ValidateAudience,
                ValidAudience = _options.ServiceId.ToString(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(_options.ClockSkewMinutes)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                _logger?.LogWarning("Token is not a valid JWT");
                return null;
            }

            // Extract claims
            var claims = new LinbikTokenClaims
            {
                Issuer = jwtToken.Issuer,
                IssuedAt = jwtToken.ValidFrom,
                ExpiresAt = jwtToken.ValidTo,
                RawClaims = jwtToken.Claims.ToDictionary(c => c.Type, c => c.Value)
            };

            // Parse standard claims
            var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            if (Guid.TryParse(subClaim, out var userId))
            {
                claims.UserId = userId;
            }

            var audClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Aud)?.Value;
            if (Guid.TryParse(audClaim, out var serviceId))
            {
                claims.ServiceId = serviceId;
            }

            var azpClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Azp)?.Value;
            if (Guid.TryParse(azpClaim, out var azp))
            {
                claims.AuthorizedParty = azp;
            }

            // Extract custom claims
            claims.UserName = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name || c.Type == "preferred_username")?.Value ?? string.Empty;
            claims.DisplayName = jwtToken.Claims.FirstOrDefault(c => c.Type == "nickname" || c.Type == "display_name")?.Value ?? claims.UserName;

            // Validate ServiceId matches this service
            if (_options.ValidateAudience && claims.ServiceId != _options.ServiceId)
            {
                _logger?.LogWarning("Token audience {Audience} does not match service {ServiceId}", claims.ServiceId, _options.ServiceId);
                return null;
            }

            _logger?.LogDebug("Token validated successfully for user {UserId}", claims.UserId);
            return claims;
        }
        catch (SecurityTokenExpiredException)
        {
            _logger?.LogDebug("Token has expired");
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger?.LogWarning("Token has invalid signature");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger?.LogWarning(ex, "Token validation failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during token validation");
            return null;
        }
    }

    /// <summary>
    /// Validate JWT token from Authorization header (async version for interface compatibility)
    /// </summary>
    public Task<ClaimsPrincipal?> ValidateTokenAsync(HttpContext context, string publicKey)
    {
        var claims = ValidateToken(context);
        if (claims == null)
            return Task.FromResult<ClaimsPrincipal?>(null);

        var claimsList = claims.RawClaims.Select(kvp => new Claim(kvp.Key, kvp.Value)).ToList();
        var identity = new ClaimsIdentity(claimsList, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult<ClaimsPrincipal?>(principal);
    }

    /// <summary>
    /// Get user ID from validated token claims
    /// </summary>
    public static Guid GetUserId(LinbikTokenClaims? claims)
    {
        return claims?.UserId ?? Guid.Empty;
    }

    /// <summary>
    /// Get user name from validated token claims
    /// </summary>
    public static string GetUserName(LinbikTokenClaims? claims)
    {
        return claims?.UserName ?? string.Empty;
    }

    /// <summary>
    /// Get display name from validated token claims
    /// </summary>
    public static string GetDisplayName(LinbikTokenClaims? claims)
    {
        return claims?.DisplayName ?? string.Empty;
    }

    /// <summary>
    /// Get service ID from validated token claims
    /// </summary>
    public static Guid GetServiceId(LinbikTokenClaims? claims)
    {
        return claims?.ServiceId ?? Guid.Empty;
    }

    /// <summary>
    /// Get authorized party (main service) from validated token claims
    /// </summary>
    public static Guid GetAuthorizedParty(LinbikTokenClaims? claims)
    {
        return claims?.AuthorizedParty ?? Guid.Empty;
    }
}
