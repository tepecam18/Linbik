using Linbik.Core.Models;

namespace Linbik.YARP.Interfaces;

/// <summary>
/// Interface for S2S (Service-to-Service) token provider
/// Manages token caching, automatic refresh, and thread-safe access
/// Supports both config-based (package name) and dynamic (service ID) targets
/// </summary>
public interface IS2STokenProvider
{
    #region Package Name Based (Config-based targets)

    /// <summary>
    /// Gets an S2S token for the specified integration service by package name
    /// Requires service to be configured in Linbik:S2STargetServices
    /// </summary>
    /// <param name="integrationPackageName">Target integration service package name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token string, or null if not available</returns>
    Task<string?> GetS2STokenAsync(string integrationPackageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets S2S tokens for multiple integration services by package names
    /// Fetches all tokens in a single request for efficiency
    /// </summary>
    /// <param name="integrationPackageNames">Target integration service package names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of package name to JWT token</returns>
    Task<IReadOnlyDictionary<string, string>> GetS2STokensAsync(
        IEnumerable<string> integrationPackageNames, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full S2S integration details including service URL by package name
    /// </summary>
    /// <param name="integrationPackageName">Target integration service package name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integration details, or null if not available</returns>
    Task<LinbikS2SIntegration?> GetS2SIntegrationAsync(
        string integrationPackageName, 
        CancellationToken cancellationToken = default);

    #endregion

    #region Service ID Based (Dynamic targets - for callbacks/webhooks)

    /// <summary>
    /// Gets an S2S token for a dynamically specified service by ID
    /// Does NOT require service to be in config - fetches directly from Linbik
    /// Use this for callbacks/webhooks where target service is not pre-configured
    /// </summary>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integration details including token and service URL, or null if not available</returns>
    Task<LinbikS2SIntegration?> GetS2SIntegrationByIdAsync(
        Guid targetServiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets S2S tokens for multiple dynamically specified services by IDs
    /// Does NOT require services to be in config
    /// </summary>
    /// <param name="targetServiceIds">Target service IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of service ID to integration details</returns>
    Task<IReadOnlyDictionary<Guid, LinbikS2SIntegration>> GetS2SIntegrationsByIdAsync(
        IEnumerable<Guid> targetServiceIds,
        CancellationToken cancellationToken = default);

    #endregion

    #region Cache Management

    /// <summary>
    /// Forces a refresh of all cached S2S tokens (config-based only)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshS2STokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached S2S tokens
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the time until the next token expiry
    /// </summary>
    /// <returns>TimeSpan until expiry, or null if no tokens cached</returns>
    TimeSpan? GetTimeUntilExpiry();

    #endregion
}
