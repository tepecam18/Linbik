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
/// Manages user sessions, token storage, and authentication flow
/// </summary>
public class LinbikAuthService : IAuthService
{
    private readonly ILinbikAuthClient _authClient;
    private readonly LinbikOptions _options;
    private readonly ILogger<LinbikAuthService> _logger;

    // Session keys
    private const string SessionKeyUserId = "linbik_user_id";
    private const string SessionKeyUserName = "linbik_user_name";
    private const string SessionKeyNickName = "linbik_nick_name";
    private const string SessionKeyRefreshToken = "linbik_refresh_token";
    private const string SessionKeyRefreshExpiresAt = "linbik_refresh_expires_at";
    private const string SessionKeyIntegrations = "linbik_integrations";

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
        
        // Store return URL in session
        if (!string.IsNullOrEmpty(returnUrl))
        {
            context.Session.SetString("linbik_return_url", returnUrl);
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
        var userId = context.Session.GetString(SessionKeyUserId);
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult<UserProfile?>(null);
        }

        var profile = new UserProfile
        {
            UserId = Guid.Parse(userId),
            UserName = context.Session.GetString(SessionKeyUserName) ?? string.Empty,
            NickName = context.Session.GetString(SessionKeyNickName) ?? string.Empty
        };

        return Task.FromResult<UserProfile?>(profile);
    }

    /// <inheritdoc />
    public Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(HttpContext context)
    {
        var json = context.Session.GetString(SessionKeyIntegrations);
        if (string.IsNullOrEmpty(json))
        {
            return Task.FromResult(new List<LinbikIntegrationToken>());
        }

        try
        {
            var tokens = JsonSerializer.Deserialize<List<LinbikIntegrationToken>>(json, JsonOptions);
            return Task.FromResult(tokens ?? new List<LinbikIntegrationToken>());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize integration tokens from session");
            return Task.FromResult(new List<LinbikIntegrationToken>());
        }
    }

    /// <inheritdoc />
    public async Task<bool> RefreshTokensAsync(HttpContext context)
    {
        var refreshToken = context.Session.GetString(SessionKeyRefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token found in session");
            return false;
        }

        var response = await _authClient.RefreshTokensAsync(refreshToken);
        if (response == null)
        {
            _logger.LogWarning("Token refresh failed");
            return false;
        }

        // Store new tokens
        StoreTokensInSession(context, response);
        return true;
    }

    /// <inheritdoc />
    public Task LogoutAsync(HttpContext context)
    {
        // Clear session
        context.Session.Remove(SessionKeyUserId);
        context.Session.Remove(SessionKeyUserName);
        context.Session.Remove(SessionKeyNickName);
        context.Session.Remove(SessionKeyRefreshToken);
        context.Session.Remove(SessionKeyRefreshExpiresAt);
        context.Session.Remove(SessionKeyIntegrations);

        // Clear integration cookies
        ClearIntegrationCookies(context);

        _logger.LogInformation("User logged out");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Store token response in session and cookies
    /// </summary>
    public void StoreTokensInSession(HttpContext context, LinbikTokenResponse response)
    {
        // Store user profile in session
        context.Session.SetString(SessionKeyUserId, response.UserId.ToString());
        context.Session.SetString(SessionKeyUserName, response.Username);
        context.Session.SetString(SessionKeyNickName, response.DisplayName);
        context.Session.SetString(SessionKeyRefreshToken, response.RefreshToken ?? string.Empty);
        context.Session.SetString(SessionKeyRefreshExpiresAt, (response.RefreshTokenExpiresAt ?? 0).ToString());

        // Store integration tokens in session
        if (response.Integrations?.Count > 0)
        {
            var integrationsJson = JsonSerializer.Serialize(response.Integrations, JsonOptions);
            context.Session.SetString(SessionKeyIntegrations, integrationsJson);

            // Store integration tokens in cookies for YARP proxy
            StoreIntegrationCookies(context, response.Integrations);
        }

        _logger.LogInformation("Tokens stored for user {UserId}", response.UserId);
    }

    #region Private Methods

    private string BuildAuthorizationUrl(string? codeChallenge = null)
    {
        var baseUrl = _options.LinbikUrl.TrimEnd('/');
        var endpoint = _options.AuthorizationEndpoint.TrimStart('/');
        var serviceId = _options.ServiceId;

        if (!string.IsNullOrEmpty(codeChallenge))
        {
            return $"{baseUrl}/{endpoint}/{serviceId}/{codeChallenge}";
        }

        return $"{baseUrl}/{endpoint}/{serviceId}";
    }

    private void StoreIntegrationCookies(HttpContext context, List<LinbikIntegrationToken> integrations)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        };

        foreach (var integration in integrations)
        {
            var cookieName = $"integration_{integration.PackageName}";
            context.Response.Cookies.Append(cookieName, integration.Token, cookieOptions);
            _logger.LogDebug("Stored integration cookie for {PackageName}", integration.PackageName);
        }
    }

    private void ClearIntegrationCookies(HttpContext context)
    {
        // Get all cookies that start with integration_
        var integrationCookies = context.Request.Cookies.Keys
            .Where(k => k.StartsWith("integration_"))
            .ToList();

        foreach (var cookieName in integrationCookies)
        {
            context.Response.Cookies.Delete(cookieName);
            _logger.LogDebug("Cleared integration cookie {CookieName}", cookieName);
        }
    }

    #endregion
}
