using Linbik.Core.Models;

namespace Linbik.YARP.Interfaces;

public interface ITokenProvider
{
    /// <summary>
    /// Gets multi-service token response from authorization code
    /// </summary>
    Task<LinbikTokenResponse?> GetMultiServiceTokenAsync(string baseUrl, string authorizationCode, string apiKey);

    /// <summary>
    /// Refreshes all service tokens using refresh token
    /// </summary>
    Task<LinbikTokenResponse?> RefreshTokensAsync(string baseUrl, string refreshToken, string apiKey, string serviceId);

    /// <summary>
    /// Gets a specific integration service token from cache
    /// </summary>
    Task<string?> GetIntegrationTokenAsync(string integrationServicePackage);

    /// <summary>
    /// Stores multi-service token response in cache
    /// </summary>
    void CacheTokenResponse(LinbikTokenResponse tokenResponse);

    /// <summary>
    /// Clears all cached tokens
    /// </summary>
    void ClearCache();
}
