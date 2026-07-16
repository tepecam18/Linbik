using Linbik.Core.Models;
using Linbik.Core.Responses;

namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// HTTP client interface for Linbik authorization server communication
/// Handles token exchange, refresh, and apps (Service-to-Service) operations
/// </summary>
public interface ILinbikAuthClient
{
    #region User-Context Token Operations

    /// <summary>
    /// Initiate authorization flow — saves auth data server-side and returns redirect URL.
    /// POST /api/oauth/initiate
    /// Headers: ApiKey
    /// Body: { clientId, codeChallenge?, returnPath? }
    /// </summary>
    /// <param name="request">Initiate request with clientId and optional PKCE/returnPath</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse wrapping success data or error details</returns>
    Task<LBaseResponse<LinbikInitiateResponse>> InitiateAuthAsync(LinbikInitiateRequest request, CancellationToken cancellationToken = default);

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

    #region Apps (Service-to-Service) Token Operations

    /// <summary>
    /// Get apps tokens for service-to-service communication (no user context)
    /// POST /api/oauth/apps-token
    /// Headers: ApiKey
    /// Body: { sourceServiceId, targetServiceIds, targetPackageNames }
    /// </summary>
    /// <param name="request">Apps token request with source service ID and target service IDs and/or package names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Apps token response with integration tokens for target services</returns>
    Task<LinbikApplicationTokenResponse?> GetApplicationTokensAsync(LinbikApplicationTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get apps tokens by target package names. Package names are sent to the server as-is;
    /// no local Linbik:AppsTargetServices configuration is required.
    /// </summary>
    /// <param name="targetPackageNames">Target service package names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Apps token response with integration tokens for target services</returns>
    Task<LinbikApplicationTokenResponse?> GetApplicationTokensAsync(IEnumerable<string> targetPackageNames, CancellationToken cancellationToken = default);

    #endregion

    #region Client Management

    /// <summary>
    /// Update a client's RedirectUri by its Name (case-insensitive).
    /// PUT /api/services/{serviceId}/clients/by-name
    /// Headers: ApiKey
    /// Body: { name, redirectUri }
    /// </summary>
    /// <param name="clientName">Client name to match (case-insensitive)</param>
    /// <param name="redirectUri">New redirect URI to set</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update succeeded, false otherwise</returns>
    Task<bool> UpdateClientRedirectUriByNameAsync(string clientName, string redirectUri, CancellationToken cancellationToken = default);

    #endregion
}
