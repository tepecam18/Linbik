using Linbik.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Interfaces;

/// <summary>
/// Core authentication service for communicating with Linbik authorization server.
/// Uses cookie-based token storage for server-side applications.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the OAuth 2.0 + PKCE flow:
/// 1. <see cref="RedirectToLinbikAsync"/> - Initiates authorization
/// 2. <see cref="ExchangeCodeForTokensAsync"/> - Exchanges code for tokens
/// 3. <see cref="RefreshTokensAsync"/> - Refreshes expired tokens
/// 4. <see cref="LogoutAsync"/> - Clears authentication state
/// </para>
/// <para>
/// All async methods support <see cref="CancellationToken"/> for graceful cancellation.
/// </para>
/// </remarks>
public interface IAuthService
{
    /// <summary>
    /// Exchanges an authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="code">The authorization code received from Linbik callback.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="LinbikTokenResponse"/> containing user profile and integration tokens,
    /// or <c>null</c> if the exchange failed.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when code is null or empty.</exception>
    /// <exception cref="HttpRequestException">Thrown when the token endpoint is unreachable.</exception>
    Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current user profile from cookies.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="UserProfile"/> if the user is authenticated, or <c>null</c> otherwise.
    /// </returns>
    Task<UserProfile?> GetUserProfileAsync(
        HttpContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the integration tokens from cookies.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A list of <see cref="LinbikIntegrationToken"/> for authorized integration services.</returns>
    Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(
        HttpContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes expired tokens using the stored refresh token.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// <c>true</c> if the token refresh was successful; <c>false</c> if refresh failed
    /// (e.g., refresh token expired or revoked).
    /// </returns>
    /// <remarks>
    /// When refresh fails, the user should be redirected to login again.
    /// </remarks>
    Task<bool> RefreshTokensAsync(
        HttpContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all authentication cookies and logs out the user.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous logout operation.</returns>
    Task LogoutAsync(
        HttpContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a user profile retrieved from Linbik authentication.
/// </summary>
/// <remarks>
/// Contains the authenticated user's identity and any integration tokens
/// for accessing third-party services.
/// </remarks>
public class UserProfile
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user's username.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's display name (nickname).
    /// </summary>
    public string NickName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the integration tokens keyed by service package name.
    /// </summary>
    /// <example>
    /// <code>
    /// var paymentToken = profile.IntegrationTokens["payment-gateway"];
    /// </code>
    /// </example>
    public Dictionary<string, string> IntegrationTokens { get; set; } = new();
}
