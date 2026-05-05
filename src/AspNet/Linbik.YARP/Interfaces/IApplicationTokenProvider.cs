using Linbik.Core.Models;

namespace Linbik.YARP.Interfaces;

/// <summary>
/// Interface for Application token provider
/// Manages token caching, automatic refresh, and thread-safe access
/// Supports both config-based (package name) and dynamic (service ID) targets
/// </summary>
public interface IApplicationTokenProvider
{
    #region Package Name Based (Config-based targets)

    /// <summary>
    /// Gets an application token for the specified integration service by package name
    /// Requires service to be configured in Linbik:S2STargetServices (kept for config compat)
    /// </summary>
    /// <param name="integrationPackageName">Target integration service package name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token string, or null if not available</returns>
    Task<string?> GetApplicationTokenAsync(string integrationPackageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets application tokens for multiple integration services by package names
    /// Fetches all tokens in a single request for efficiency
    /// </summary>
    /// <param name="integrationPackageNames">Target integration service package names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of package name to JWT token</returns>
    Task<IReadOnlyDictionary<string, string>> GetApplicationTokensAsync(
        IEnumerable<string> integrationPackageNames, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full application integration details including service URL by package name
    /// </summary>
    /// <param name="integrationPackageName">Target integration service package name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integration details, or null if not available</returns>
    Task<LinbikApplicationIntegration?> GetApplicationIntegrationAsync(
        string integrationPackageName, 
        CancellationToken cancellationToken = default);

    #endregion

    #region Service ID Based (Dynamic targets - for callbacks/webhooks)

    /// <summary>
    /// Gets an application token for a dynamically specified service by ID
    /// Does NOT require service to be in config - fetches directly from Linbik
    /// Use this for callbacks/webhooks where target service is not pre-configured
    /// </summary>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integration details including token and service URL, or null if not available</returns>
    Task<LinbikApplicationIntegration?> GetApplicationIntegrationByIdAsync(
        Guid targetServiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets application tokens for multiple dynamically specified services by IDs
    /// Does NOT require services to be in config
    /// </summary>
    /// <param name="targetServiceIds">Target service IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of service ID to integration details</returns>
    Task<IReadOnlyDictionary<Guid, LinbikApplicationIntegration>> GetApplicationIntegrationsByIdAsync(
        IEnumerable<Guid> targetServiceIds,
        CancellationToken cancellationToken = default);

    #endregion

    #region Cache Management

    /// <summary>
    /// Forces a refresh of all cached application tokens (config-based only)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshApplicationTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached application tokens
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the time until the next token expiry
    /// </summary>
    /// <returns>TimeSpan until expiry, or null if no tokens cached</returns>
    TimeSpan? GetTimeUntilExpiry();

    #endregion
}
