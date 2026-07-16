using Linbik.Core.Services.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Linbik.Server.Services;

/// <summary>
/// PASETO v4.public doğrulama servisi.
/// Linbik.App tarafından imzalanmış Ed25519 token'larını doğrular.
/// Token üretmez; yalnızca doğrulama yapar.
/// </summary>
public sealed class IntegrationTokenValidator
{
    private readonly ServerOptions _options;
    private readonly IPasetoHelper _pasetoHelper;
    private readonly ILogger<IntegrationTokenValidator>? _logger;

    public IntegrationTokenValidator(
        IOptions<ServerOptions> options,
        IPasetoHelper pasetoHelper,
        ILogger<IntegrationTokenValidator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pasetoHelper);
        _options = options.Value;
        _pasetoHelper = pasetoHelper;
        _logger = logger;
    }

    /// <summary>
    /// Doğrulayıcının kullanıma hazır olup olmadığını döner.
    /// Health check tarafından kullanılır.
    /// </summary>
    public bool IsConfigured() => !string.IsNullOrEmpty(_options.PublicKey);

    /// <summary>
    /// Authorization header'dan PASETO token'ı çıkarıp doğrular.
    /// </summary>
    public LinbikTokenClaims? ValidateToken(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("Missing or invalid Authorization header");
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        return ValidateToken(token);
    }

    /// <summary>
    /// Validate JWT token string
    /// </summary>
    /// <param name="token">PASETO token string'i</param>
    /// <returns>Token claims if valid, null if invalid</returns>
    public LinbikTokenClaims? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger?.LogDebug("Empty token provided");
            return null;
        }

        if (!IsConfigured())
        {
            _logger?.LogError("No Ed25519 public key configured for PASETO validation");
            return null;
        }

        // ValidateTokenAsync is CPU-bound (Ed25519 verify), GetAwaiter().GetResult() is safe here
        var isValid = _pasetoHelper
            .ValidateTokenAsync(token, _options.PublicKey, _options.PackageName, _options.JwtIssuer)
            .GetAwaiter().GetResult();

        if (!isValid)
        {
            _logger?.LogWarning("PASETO token validation failed for service {PackageName}", _options.PackageName);
            return null;
        }

        var rawClaims = _pasetoHelper.GetTokenClaims(token);
        if (rawClaims.Count == 0)
        {
            _logger?.LogWarning("Could not extract claims from PASETO token");
            return null;
        }

        var claims = new LinbikTokenClaims
        {
            Issuer = rawClaims.GetValueOrDefault("iss") ?? string.Empty,
            IssuedAt = ParseIso8601(rawClaims.GetValueOrDefault("iat")),
            ExpiresAt = ParseIso8601(rawClaims.GetValueOrDefault("exp")),
            RawClaims = rawClaims,
        };

        var tokenType = rawClaims.GetValueOrDefault("token_type");
        var hasUserClaims = rawClaims.ContainsKey("name") || rawClaims.ContainsKey("preferred_username");

        if (tokenType == "apps" || !hasUserClaims)
        {
            claims.TokenType = LinbikTokenType.Application;
            var sub = rawClaims.GetValueOrDefault("sub");
            if (Guid.TryParse(sub, out var sourceServiceId))
                claims.SourceServiceId = sourceServiceId;
            claims.SourcePackageName = rawClaims.GetValueOrDefault("source_package_name");
            _logger?.LogDebug("PASETO application token validated for source {SourceServiceId}", claims.SourceServiceId);
        }
        else
        {
            claims.TokenType = LinbikTokenType.Delegated;
            var sub = rawClaims.GetValueOrDefault("sub");
            if (Guid.TryParse(sub, out var userId))
                claims.UserId = userId;
            claims.UserName = rawClaims.GetValueOrDefault("preferred_username")
                           ?? rawClaims.GetValueOrDefault("name");
            claims.DisplayName = rawClaims.GetValueOrDefault("nickname")
                              ?? rawClaims.GetValueOrDefault("display_name")
                              ?? claims.UserName;
            _logger?.LogDebug("PASETO delegated token validated for user {UserId}", claims.UserId);
        }

        var azp = rawClaims.GetValueOrDefault("azp");
        if (Guid.TryParse(azp, out var authorizedParty))
            claims.AuthorizedParty = authorizedParty;

        claims.PackageName = rawClaims.GetValueOrDefault("aud") ?? string.Empty;

        if (_options.ValidateAudience && claims.PackageName != _options.PackageName)
        {
            _logger?.LogWarning(
                "Token audience '{Audience}' does not match service '{PackageName}'",
                claims.PackageName, _options.PackageName);
            return null;
        }

        return claims;
    }

    /// <summary>
    /// Authorization header'dan async PASETO doğrulama (ClaimsPrincipal için).
    /// </summary>
    public Task<ClaimsPrincipal?> ValidateTokenAsync(HttpContext context)
    {
        var tokenClaims = ValidateToken(context);
        if (tokenClaims is null)
            return Task.FromResult<ClaimsPrincipal?>(null);

        var claimsList = tokenClaims.RawClaims.Select(kvp => new Claim(kvp.Key, kvp.Value)).ToList();
        var identity = new ClaimsIdentity(claimsList, "Bearer");
        return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private static DateTime ParseIso8601(string? value)
    {
        if (string.IsNullOrEmpty(value)) return DateTime.MinValue;
        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt;
        return DateTime.MinValue;
    }

    /// <summary>
    /// Get token type from validated token claims
    /// </summary>
    public static LinbikTokenType GetTokenType(LinbikTokenClaims? claims) =>
        claims?.TokenType ?? LinbikTokenType.Delegated;

    /// <summary>
    /// Get user ID from validated token claims (UserService tokens only)
    /// </summary>
    public static Guid GetUserId(LinbikTokenClaims? claims) =>
        claims?.UserId ?? Guid.Empty;

    /// <summary>
    /// Get user name from validated token claims (UserService tokens only)
    /// </summary>
    public static string GetUserName(LinbikTokenClaims? claims) =>
        claims?.UserName ?? string.Empty;

    /// <summary>
    /// Get display name from validated token claims (UserService tokens only)
    /// </summary>
    public static string GetDisplayName(LinbikTokenClaims? claims) =>
        claims?.DisplayName ?? string.Empty;

    /// <summary>
    /// Get source service ID from validated token claims (application tokens only)
    /// </summary>
    public static Guid GetSourceServiceId(LinbikTokenClaims? claims) =>
        claims?.SourceServiceId ?? Guid.Empty;

    /// <summary>
    /// Get source package name from validated token claims (application tokens only)
    /// </summary>
    public static string GetSourcePackageName(LinbikTokenClaims? claims) =>
        claims?.SourcePackageName ?? string.Empty;

    /// <summary>
    /// Get service ID from validated token claims
    /// </summary>
    public static string GetPackageName(LinbikTokenClaims? claims) =>
        claims?.PackageName ?? string.Empty;

    /// <summary>
    /// Get authorized party (main service) from validated token claims
    /// </summary>
    public static Guid GetAuthorizedParty(LinbikTokenClaims? claims) =>
        claims?.AuthorizedParty ?? Guid.Empty;
}
