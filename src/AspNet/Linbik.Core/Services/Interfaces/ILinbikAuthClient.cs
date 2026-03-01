using Linbik.Core.Models;

namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// HTTP client interface for Linbik authorization server communication
/// Handles token exchange, refresh, and S2S (Service-to-Service) operations
/// </summary>
public interface ILinbikAuthClient
{
    #region User-Context Token Operations

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

    #endregion

    #region S2S (Service-to-Service) Token Operations

    /// <summary>
    /// Get S2S tokens for service-to-service communication (no user context)
    /// POST /auth/s2s-token
    /// Headers: ApiKey
    /// Body: { sourceServiceId, targetServiceIds }
    /// </summary>
    /// <param name="request">S2S token request with source and target service IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>S2S token response with integration tokens for target services</returns>
    Task<LinbikS2STokenResponse?> GetS2STokensAsync(LinbikS2STokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get S2S tokens by target package names (uses configured service ID mapping)
    /// Requires Linbik:S2STargetServices configuration
    /// </summary>
    /// <param name="targetPackageNames">Target service package names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>S2S token response with integration tokens for target services</returns>
    Task<LinbikS2STokenResponse?> GetS2STokensAsync(IEnumerable<string> targetPackageNames, CancellationToken cancellationToken = default);

    #endregion
}
