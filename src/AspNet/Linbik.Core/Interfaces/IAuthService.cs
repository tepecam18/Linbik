using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Interfaces;

/// <summary>
/// Core authentication service for communicating with Linbik authorization server
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Legacy: Get user ID from context (deprecated)
    /// </summary>
    [Obsolete("Use GetUserProfileAsync instead")]
    Task<string> GetUserIdAsync(HttpContext context);

    /// <summary>
    /// Redirect user to Linbik authorization endpoint
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="returnUrl">URL to return after authentication</param>
    /// <param name="codeChallenge">Optional PKCE code challenge</param>
    Task RedirectToLinbikAsync(HttpContext context, string? returnUrl = null, string? codeChallenge = null);

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    /// <param name="code">Authorization code from callback</param>
    /// <returns>Token response with user profile and integration tokens</returns>
    Task<TokenResponse?> ExchangeCodeForTokensAsync(string code);

    /// <summary>
    /// Get current user profile from session
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>User profile or null if not authenticated</returns>
    Task<UserProfile?> GetUserProfileAsync(HttpContext context);

    /// <summary>
    /// Get integration tokens from session
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>List of integration tokens</returns>
    Task<List<IntegrationToken>> GetIntegrationTokensAsync(HttpContext context);

    /// <summary>
    /// Refresh expired tokens using refresh token
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if refresh successful</returns>
    Task<bool> RefreshTokensAsync(HttpContext context);

    /// <summary>
    /// Clear authentication session
    /// </summary>
    /// <param name="context">HTTP context</param>
    Task LogoutAsync(HttpContext context);
}

/// <summary>
/// Token response from Linbik authorization server
/// </summary>
public class TokenResponse
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public List<IntegrationToken> Integrations { get; set; } = new();
    public string RefreshToken { get; set; } = string.Empty;
    public long RefreshTokenExpiresAt { get; set; }
    public string? CodeChallenge { get; set; }
}

/// <summary>
/// Integration service token
/// </summary>
public class IntegrationToken
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ServicePackage { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty; // Alias for ServicePackage
    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty; // Alias for Token
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// User profile from session
/// </summary>
public class UserProfile
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public Dictionary<string, string> IntegrationTokens { get; set; } = new();
}
