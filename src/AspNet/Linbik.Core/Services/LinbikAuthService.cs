using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Core.Services;

/// <summary>
/// Main authentication service for Linbik
/// Cookie-based authentication - profile info extracted from JWT for security
/// </summary>
public class LinbikAuthService : IAuthService
{
    private readonly ILinbikAuthClient _authClient;
    private readonly LinbikOptions _options;
    private readonly ILogger<LinbikAuthService> _logger;

    // Cookie names
    private const string AuthTokenCookie = "authToken";
    private const string RefreshTokenCookie = "linbikRefreshToken";
    private const string IntegrationTokenPrefix = "integration_";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LinbikAuthService(
        ILinbikAuthClient authClient,
        IOptions<LinbikOptions> options,
        ILogger<LinbikAuthService> logger)
    {
        _authClient = authClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RedirectToLinbikAsync(HttpContext context, string? returnUrl = null, string? codeChallenge = null)
    {
        var authUrl = BuildAuthorizationUrl(codeChallenge);
        
        // Store return URL in cookie (not session)
        if (!string.IsNullOrEmpty(returnUrl))
        {
            context.Response.Cookies.Append("linbik_return_url", returnUrl, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/"
            });
        }

        context.Response.Redirect(authUrl);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(string code)
    {
        var response = await _authClient.ExchangeCodeAsync(code);
        if (response == null)
        {
            _logger.LogWarning("Token exchange failed for code");
            return null;
        }

        return response;
    }

    /// <inheritdoc />
    public Task<UserProfile?> GetUserProfileAsync(HttpContext context)
    {
        // Extract profile from JWT token (more secure than storing in session/cookie)
        var authToken = context.Request.Cookies[AuthTokenCookie];
        if (string.IsNullOrEmpty(authToken))
        {
            return Task.FromResult<UserProfile?>(null);
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(authToken);

            var userId = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
            var userName = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name" || c.Type == "preferred_username")?.Value;
            var nickName = jwt.Claims.FirstOrDefault(c => c.Type == "nickname" || c.Type == "display_name")?.Value;

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
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
    public Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(HttpContext context)
    {
        // Get integration tokens from cookies
        var tokens = new List<LinbikIntegrationToken>();

        foreach (var cookie in context.Request.Cookies)
        {
            if (cookie.Key.StartsWith(IntegrationTokenPrefix))
            {
                var packageName = cookie.Key.Substring(IntegrationTokenPrefix.Length);
                tokens.Add(new LinbikIntegrationToken
                {
                    PackageName = packageName,
                    ServiceName = packageName,
                    Token = cookie.Value,
                    ServiceUrl = string.Empty // URL not stored in cookie for security
                });
            }
        }

        return Task.FromResult(tokens);
    }

    /// <inheritdoc />
    public async Task<bool> RefreshTokensAsync(HttpContext context)
    {
        // Get refresh token from cookie
        var refreshToken = context.Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token found in cookies");
            return false;
        }

        var response = await _authClient.RefreshTokensAsync(refreshToken);
        if (response == null)
        {
            _logger.LogWarning("Token refresh failed");
            return false;
        }

        // Store new tokens in cookies
        StoreTokensInCookies(context, response);
        return true;
    }

    /// <inheritdoc />
    public Task LogoutAsync(HttpContext context)
    {
        var deleteCookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None
        };

        // Clear auth cookies
        context.Response.Cookies.Delete(AuthTokenCookie, deleteCookieOptions);
        context.Response.Cookies.Delete(RefreshTokenCookie, deleteCookieOptions);

        // Clear integration cookies
        ClearIntegrationCookies(context);

        _logger.LogInformation("User logged out - cookies cleared");
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

    private string BuildAuthorizationUrl(string? codeChallenge = null)
    {
        var baseUrl = _options.LinbikUrl.TrimEnd('/');
        var endpoint = _options.AuthorizationEndpoint.TrimStart('/');
        var clientId = _options.ClientId;

        if (!string.IsNullOrEmpty(codeChallenge))
        {
            return $"{baseUrl}/{endpoint}/{clientId}/{codeChallenge}";
        }

        return $"{baseUrl}/{endpoint}/{clientId}";
    }

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
}
