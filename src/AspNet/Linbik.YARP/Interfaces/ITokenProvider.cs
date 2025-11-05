using Linbik.Core.Models;

namespace Linbik.YARP.Interfaces;

public interface ITokenProvider
{
    /// <summary>
    /// Legacy: Gets a single JWT token (deprecated - use GetMultiServiceTokenAsync)
    /// </summary>
    [Obsolete("Use GetMultiServiceTokenAsync for OAuth 2.0 flow with multi-service support")]
    Task<string> GetTokenAsync(string baseUrl, string clientId, string clientSecret);

    /// <summary>
    /// Gets multi-service token response from authorization code
    /// </summary>
    /// <param name="baseUrl">Linbik server base URL</param>
    /// <param name="authorizationCode">Authorization code from callback</param>
    /// <param name="apiKey">Service API key</param>
    /// <returns>Multi-service token response</returns>
    Task<MultiServiceTokenResponse?> GetMultiServiceTokenAsync(string baseUrl, string authorizationCode, string apiKey);

    /// <summary>
    /// Refreshes all service tokens using refresh token
    /// </summary>
    /// <param name="baseUrl">Linbik server base URL</param>
    /// <param name="refreshToken">Refresh token</param>
    /// <param name="apiKey">Service API key</param>
    /// <param name="serviceId">Service ID (community ID)</param>
    /// <returns>New multi-service token response</returns>
    Task<MultiServiceTokenResponse?> RefreshTokensAsync(string baseUrl, string refreshToken, string apiKey, string serviceId);

    /// <summary>
    /// Gets a specific integration service token from cache or refreshes if expired
    /// </summary>
    /// <param name="integrationServicePackage">Integration service package name (e.g., "payment-gateway")</param>
    /// <returns>JWT token for the integration service</returns>
    Task<string?> GetIntegrationTokenAsync(string integrationServicePackage);

    /// <summary>
    /// Stores multi-service token response in cache
    /// </summary>
    /// <param name="tokenResponse">Token response from Linbik</param>
    void CacheTokenResponse(MultiServiceTokenResponse tokenResponse);

    /// <summary>
    /// Clears all cached tokens
    /// </summary>
    void ClearCache();
}
