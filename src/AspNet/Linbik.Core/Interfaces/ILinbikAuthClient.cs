using Linbik.Core.Models;

namespace Linbik.Core.Interfaces;

/// <summary>
/// HTTP client interface for Linbik authorization server communication
/// Handles token exchange and refresh operations
/// </summary>
public interface ILinbikAuthClient
{
    /// <summary>
    /// Exchange authorization code for tokens
    /// POST /auth/token
    /// Headers: ApiKey, Code
    /// </summary>
    /// <param name="code">Authorization code from callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response with user profile and integration tokens</returns>
    Task<LinbikTokenResponse?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh expired tokens using refresh token
    /// POST /auth/refresh
    /// Headers: ApiKey, RefreshToken
    /// </summary>
    /// <param name="refreshToken">Refresh token from previous token response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New token response with refreshed tokens</returns>
    Task<LinbikTokenResponse?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default);
}
