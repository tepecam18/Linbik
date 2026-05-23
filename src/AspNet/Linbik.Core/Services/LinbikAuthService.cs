using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Linbik.Core.Services;

/// <summary>
/// Main authentication service for Linbik.
/// </summary>
/// <remarks>
/// <para>
/// This service implements cookie-based authentication where profile information
/// is extracted from the JWT token for enhanced security. No sensitive data is
/// stored in plain cookies.
/// </para>
/// <para>
/// Cookie Strategy:
/// <list type="bullet">
///   <item><description><c>authToken</c>: JWT access token (HttpOnly, Secure)</description></item>
///   <item><description><c>linbikRefreshToken</c>: Refresh token (HttpOnly, Secure)</description></item>
///   <item><description><c>integration_{packageName}</c>: Per-service integration tokens</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class LinbikAuthService(
    ILinbikAuthClient authClient,
    IOptions<LinbikOptions> options,
    ILogger<LinbikAuthService> logger) : IAuthService
{
    private readonly ILinbikAuthClient _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
    private readonly LinbikOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<LinbikAuthService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Cookie names from LinbikDefaults
    private const string AuthTokenCookie = LinbikDefaults.AuthTokenCookie;
    private const string RefreshTokenCookie = LinbikDefaults.RefreshTokenCookie;
    private const string IntegrationTokenPrefix = LinbikDefaults.IntegrationTokenPrefix;
    private const string ReturnUrlCookie = LinbikDefaults.ReturnUrlCookie;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var response = await _authClient.ExchangeCodeAsync(code, cancellationToken);
        if (response == null)
        {
            _logger.LogWarning("Token exchange failed for authorization code");
            return null;
        }

        return response;
    }

    /// <inheritdoc />
    public Task<UserProfile?> GetUserProfileAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Extract profile from JWT token (more secure than storing in session/cookie)
        var authToken = context.Request.Cookies[AuthTokenCookie];
        if (string.IsNullOrEmpty(authToken))
        {
            return Task.FromResult<UserProfile?>(null);
        }

        try
        {
            // Unsafe read — only parses claims without signature validation.
            // The cookie token is HS256 JWT; payload is base64url-encoded JSON at segment [1].
            var claims = ReadJwtPayloadClaims(authToken);

            var userId = claims.GetValueOrDefault(ClaimTypes.NameIdentifier)
                      ?? claims.GetValueOrDefault("sub");
            var userName = claims.GetValueOrDefault(ClaimTypes.Name)
                        ?? claims.GetValueOrDefault("name")
                        ?? claims.GetValueOrDefault("preferred_username");
            var nickName = claims.GetValueOrDefault("nickname")
                        ?? claims.GetValueOrDefault("display_name");

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogDebug("Invalid or missing user ID in JWT token");
                return Task.FromResult<UserProfile?>(null);
            }

            var profile = new UserProfile
            {
                UserId = userGuid,
                UserName = userName ?? string.Empty,
                NickName = nickName ?? userName ?? string.Empty
            };

            return Task.FromResult<UserProfile?>(profile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JWT token for user profile");
            return Task.FromResult<UserProfile?>(null);
        }
    }

    /// <inheritdoc />
    public Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get integration tokens from cookies
        List<LinbikIntegrationToken> tokens = [];

        foreach (var cookie in context.Request.Cookies)
        {
            if (cookie.Key.StartsWith(IntegrationTokenPrefix))
            {
                var packageName = cookie.Key[IntegrationTokenPrefix.Length..];
                tokens.Add(new LinbikIntegrationToken
                {
                    PackageName = packageName,
                    ServiceName = packageName,
                    Token = cookie.Value,
                    ServiceUrl = string.Empty // URL not stored in cookie for security
                });
            }
        }

        _logger.LogDebug("Retrieved {Count} integration tokens from cookies", tokens.Count);
        return Task.FromResult(tokens);
    }

    /// <inheritdoc />
    public async Task<bool> RefreshTokensAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        // Get refresh token from cookie
        var refreshToken = context.Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token found in cookies");
            return false;
        }

        var response = await _authClient.RefreshTokensAsync(refreshToken, cancellationToken);
        if (response == null)
        {
            _logger.LogWarning("Token refresh failed");
            return false;
        }

        // Store new tokens in cookies
        StoreTokensInCookies(context, response);
        _logger.LogInformation("Tokens refreshed successfully for user {UserId}", response.UserId);
        return true;
    }

    /// <inheritdoc />
    public Task LogoutAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deleteCookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None
        };

        // Clear auth cookies
        context.Response.Cookies.Delete(AuthTokenCookie, deleteCookieOptions);
        context.Response.Cookies.Delete(RefreshTokenCookie, deleteCookieOptions);
        context.Response.Cookies.Delete(ReturnUrlCookie, deleteCookieOptions);

        // Clear integration cookies
        ClearIntegrationCookies(context);

        _logger.LogInformation("User logged out - all cookies cleared");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Store token response in cookies (no session required)
    /// Profile info is NOT stored - it's extracted from JWT when needed
    /// </summary>
    public void StoreTokensInCookies(HttpContext context, LinbikTokenResponse response)
    {
        var secureCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        };

        // Store refresh token if available
        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            var refreshExpiry = (response.RefreshTokenExpiresAt ?? 0) > 0
                ? DateTimeOffset.FromUnixTimeSeconds(response.RefreshTokenExpiresAt!.Value).UtcDateTime
                : DateTime.UtcNow.AddDays(14);

            context.Response.Cookies.Append(RefreshTokenCookie, response.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = refreshExpiry
            });
        }

        // Store integration tokens in cookies for YARP proxy
        if (response.Integrations?.Count > 0)
        {
            StoreIntegrationCookies(context, response.Integrations);
        }

        _logger.LogInformation("Tokens stored in cookies for user {UserId}", response.UserId);
    }

    #region Private Methods

    private void StoreIntegrationCookies(HttpContext context, List<LinbikIntegrationToken> integrations)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        };

        foreach (var integration in integrations)
        {
            var cookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
            context.Response.Cookies.Append(cookieName, integration.Token, cookieOptions);
            _logger.LogDebug("Stored integration cookie for {PackageName}", integration.PackageName);
        }
    }

    private void ClearIntegrationCookies(HttpContext context)
    {
        var deleteCookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None
        };

        // Get all cookies that start with integration_
        var integrationCookies = context.Request.Cookies.Keys
            .Where(k => k.StartsWith(IntegrationTokenPrefix))
            .ToList();

        foreach (var cookieName in integrationCookies)
        {
            context.Response.Cookies.Delete(cookieName, deleteCookieOptions);
            _logger.LogDebug("Cleared integration cookie {CookieName}", cookieName);
        }
    }

    #endregion

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// JWT payload'ını imza doğrulaması yapmadan okur (cookie-based HS256 token için).
    /// JWT: header.payload.signature — payload base64url-encoded JSON'dur.
    /// </summary>
    private static Dictionary<string, string> ReadJwtPayloadClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 3) return [];

        try
        {
            var base64 = parts[1].Replace('-', '+').Replace('_', '/');
            base64 = (base64.Length % 4) switch
            {
                2 => base64 + "==",
                3 => base64 + "=",
                _ => base64,
            };

            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

            using var doc = JsonDocument.Parse(payloadJson);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();
            }
            return result;
        }
        catch
        {
            return [];
        }
    }
}
