using Linbik.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Interfaces;

/// <summary>
/// Core authentication service for communicating with Linbik authorization server
/// Uses session-based token storage for server-side applications
/// </summary>
public interface IAuthService
{
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
    Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(string code);

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
    Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(HttpContext context);

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
/// User profile from session
/// </summary>
public class UserProfile
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public Dictionary<string, string> IntegrationTokens { get; set; } = new();
}
