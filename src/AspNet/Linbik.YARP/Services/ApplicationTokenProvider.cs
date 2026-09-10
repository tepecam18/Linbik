using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.YARP.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Linbik.YARP.Services;

/// <summary>
/// Application token provider with automatic caching and refresh
/// Uses Linbik.Core's ILinbikAuthClient for HTTP operations
/// Supports both config-based (package name) and dynamic (service ID) targets
/// </summary>
public sealed class ApplicationTokenProvider : IApplicationTokenProvider, IDisposable
{
    private readonly ILinbikAuthClient _authClient;
    private readonly LinbikOptions _options;
    private readonly LinbikProvisionClient _provisionClient;
    private readonly ILogger<ApplicationTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Timer? _autoRefreshTimer;

    // Cache for application tokens - by package name (config-based)
    private readonly ConcurrentDictionary<string, ApplicationTokenCacheItem> _tokenCache = new();
    // Cache for application tokens - by package name (dynamic targets, keyed the same way since
    // the server only ever identifies a token by its target's package name)
    private readonly ConcurrentDictionary<string, ApplicationTokenCacheItem> _dynamicTokenCache = new();
    // Reverse index so dynamic (ID-based) lookups can find their cached token by package name
    private readonly ConcurrentDictionary<Guid, string> _dynamicIdToPackageName = new();
    private DateTime _cacheExpiry = DateTime.MinValue;

    private sealed class ApplicationTokenCacheItem
    {
        public required LinbikApplicationIntegration Integration { get; init; }
        public DateTime ExpiresAt { get; init; }
        public DateTime FetchedAt { get; init; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool NeedsRefresh(double threshold) =>
            DateTime.UtcNow >= FetchedAt.Add(TimeSpan.FromTicks((long)((ExpiresAt - FetchedAt).Ticks * threshold)));
    }

    public ApplicationTokenProvider(
        ILinbikAuthClient authClient,
        IOptions<LinbikOptions> options,
        LinbikProvisionClient provisionClient,
        ILogger<ApplicationTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(authClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provisionClient);
        ArgumentNullException.ThrowIfNull(logger);

        _authClient = authClient;
        _options = options.Value;
        _provisionClient = provisionClient;
        _logger = logger;

        // Setup auto-refresh timer if enabled (only for config-based services)
        if (_options.AppsAutoRefresh && _options.AppsTargetServices.Count > 0)
        {
            var refreshInterval = TimeSpan.FromMinutes(_options.AppsTokenLifetimeMinutes * _options.AppsRefreshThreshold);
            _autoRefreshTimer = new Timer(
                async _ => await AutoRefreshTokensAsync(),
                null,
                refreshInterval,
                refreshInterval);

            _logger.LogInformation("Application token auto-refresh enabled with interval: {Interval}", refreshInterval);
        }
    }

    #region Package Name Based (Config-based targets)

    /// <inheritdoc />
    public async Task<string?> GetApplicationTokenAsync(string integrationPackageName, CancellationToken cancellationToken = default)
    {
        var integration = await GetApplicationIntegrationAsync(integrationPackageName, cancellationToken);
        return integration?.Token;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetApplicationTokensAsync(
        IEnumerable<string> integrationPackageNames,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        var packageNames = integrationPackageNames.ToList();

        // Check which tokens we need to fetch
        var needFetch = new List<string>();
        foreach (var packageName in packageNames)
        {
            if (_tokenCache.TryGetValue(packageName, out var cached) && !cached.IsExpired)
            {
                result[packageName] = cached.Integration.Token;
            }
            else
            {
                needFetch.Add(packageName);
            }
        }

        // Fetch missing tokens
        if (needFetch.Count > 0)
        {
            await FetchAndCacheTokensByPackageAsync(needFetch, cancellationToken);

            // Add newly fetched tokens to result
            foreach (var packageName in needFetch)
            {
                if (_tokenCache.TryGetValue(packageName, out var cached))
                {
                    result[packageName] = cached.Integration.Token;
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<LinbikApplicationIntegration?> GetApplicationIntegrationAsync(
        string integrationPackageName,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_tokenCache.TryGetValue(integrationPackageName, out var cached))
        {
            if (!cached.IsExpired)
            {
                // Check if needs proactive refresh
                if (cached.NeedsRefresh(_options.AppsRefreshThreshold))
                {
                    _logger.LogDebug("Application token for {Package} needs refresh (threshold: {Threshold}%)",
                        integrationPackageName, _options.AppsRefreshThreshold * 100);

                    // Trigger background refresh but return current token
                    _ = Task.Run(async () => await RefreshApplicationTokensAsync(cancellationToken), cancellationToken);
                }

                return cached.Integration;
            }
            else
            {
                _logger.LogDebug("Application token for {Package} expired", integrationPackageName);
            }
        }

        // Need to fetch token
        await FetchAndCacheTokensByPackageAsync([integrationPackageName], cancellationToken);

        if (_tokenCache.TryGetValue(integrationPackageName, out cached))
        {
            return cached.Integration;
        }

        _logger.LogWarning("Failed to obtain application token for {Package}", integrationPackageName);
        return null;
    }

    #endregion

    #region Service ID Based (Dynamic targets)

    /// <inheritdoc />
    public async Task<LinbikApplicationIntegration?> GetApplicationIntegrationByIdAsync(
        Guid targetServiceId,
        CancellationToken cancellationToken = default)
    {
        // Check dynamic cache first (only possible once we've previously discovered its package name)
        if (_dynamicIdToPackageName.TryGetValue(targetServiceId, out var packageName) &&
            _dynamicTokenCache.TryGetValue(packageName, out var cached))
        {
            if (!cached.IsExpired)
            {
                // Check if needs proactive refresh
                if (cached.NeedsRefresh(_options.AppsRefreshThreshold))
                {
                    _logger.LogDebug("Dynamic application token for {ServiceId} needs refresh", targetServiceId);

                    // Trigger background refresh but return current token
                    _ = Task.Run(async () => await FetchAndCacheDynamicTokensAsync(targetServiceId, default), cancellationToken);
                }

                return cached.Integration;
            }
            else
            {
                _logger.LogDebug("Dynamic application token for {ServiceId} expired", targetServiceId);
            }
        }

        // Need to fetch token
        await FetchAndCacheDynamicTokensAsync(targetServiceId, cancellationToken);

        if (_dynamicIdToPackageName.TryGetValue(targetServiceId, out packageName) &&
            _dynamicTokenCache.TryGetValue(packageName, out cached))
        {
            return cached.Integration;
        }

        _logger.LogWarning("Failed to obtain dynamic application token for service {ServiceId}", targetServiceId);
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LinbikApplicationIntegration>> GetApplicationIntegrationsByIdAsync(
        IEnumerable<Guid> targetServiceIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, LinbikApplicationIntegration>();

        // The server only identifies a token by its target's package name (not the requested ID),
        // so each ID is resolved individually to correlate results correctly.
        foreach (var serviceId in targetServiceIds.Distinct())
        {
            var integration = await GetApplicationIntegrationByIdAsync(serviceId, cancellationToken);
            if (integration != null)
            {
                result[serviceId] = integration;
            }
        }

        return result;
    }

    #endregion

    #region Cache Management

    /// <inheritdoc />
    public async Task RefreshApplicationTokensAsync(CancellationToken cancellationToken = default)
    {
        var packageNames = _options.AppsTargetServices.Keys.ToList();
        if (packageNames.Count == 0)
        {
            _logger.LogWarning("No application target services configured");
            return;
        }

        await FetchAndCacheTokensByPackageAsync(packageNames, cancellationToken);
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _tokenCache.Clear();
        _dynamicTokenCache.Clear();
        _dynamicIdToPackageName.Clear();
        _cacheExpiry = DateTime.MinValue;
        _logger.LogInformation("Application token cache cleared (both config-based and dynamic)");
    }

    /// <inheritdoc />
    public TimeSpan? GetTimeUntilExpiry()
    {
        if (_tokenCache.IsEmpty && _dynamicTokenCache.IsEmpty)
            return null;

        // Find earliest expiry across both caches
        DateTime? earliest = null;

        foreach (var item in _tokenCache.Values)
        {
            if (earliest == null || item.ExpiresAt < earliest)
                earliest = item.ExpiresAt;
        }

        foreach (var item in _dynamicTokenCache.Values)
        {
            if (earliest == null || item.ExpiresAt < earliest)
                earliest = item.ExpiresAt;
        }

        if (earliest == null)
            return null;

        var remaining = earliest.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    #endregion

    #region Private Methods

    private async Task FetchAndCacheTokensByPackageAsync(
        IEnumerable<string> packageNames,
        CancellationToken cancellationToken)
    {
        var requestedPackageNames = packageNames.Distinct().ToList();
        if (requestedPackageNames.Count == 0)
        {
            _logger.LogError("No application target package names found");
            return;
        }

        await EnsureKeylessProvisionedAsync(cancellationToken);

        // Acquire lock to prevent concurrent fetches
        if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            _logger.LogWarning("Application token fetch timed out waiting for lock");
            return;
        }

        try
        {
            // Double-check cache after acquiring lock
            var stillNeedFetch = requestedPackageNames
                .Where(packageName => !_tokenCache.TryGetValue(packageName, out var c) || c.IsExpired)
                .ToList();

            if (stillNeedFetch.Count == 0)
            {
                _logger.LogDebug("Application tokens already refreshed by another thread");
                return;
            }

            var request = new LinbikApplicationTokenRequest
            {
                SourceServiceId = Guid.Parse(_options.ServiceId),
                TargetPackageNames = stillNeedFetch
            };

            _logger.LogDebug("Fetching config-based application tokens for {Count} services", stillNeedFetch.Count);

            var response = await _authClient.GetApplicationTokensAsync(request, cancellationToken);

            if (response?.Integrations == null)
            {
                _logger.LogWarning("Application token response was null or empty");
                return;
            }

            var now = DateTime.UtcNow;
            // AccessTokenExpiresAt is Unix timestamp (seconds since epoch)
            var expiry = response.AccessTokenExpiresAt > 0
                ? DateTimeOffset.FromUnixTimeSeconds(response.AccessTokenExpiresAt).UtcDateTime
                : now.AddMinutes(_options.AppsTokenLifetimeMinutes);

            foreach (var integration in response.Integrations)
            {
                var cacheItem = new ApplicationTokenCacheItem
                {
                    Integration = integration,
                    ExpiresAt = expiry,
                    FetchedAt = now
                };

                // Cache by package name
                _tokenCache.AddOrUpdate(integration.PackageName, cacheItem, (_, _) => cacheItem);
            }

            _cacheExpiry = expiry;

            _logger.LogInformation("Cached config-based application tokens for {Count} services, expires at {Expiry}",
                response.Integrations.Count, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch application tokens");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task FetchAndCacheDynamicTokensAsync(
        Guid targetServiceId,
        CancellationToken cancellationToken)
    {
        await EnsureKeylessProvisionedAsync(cancellationToken);

        // Acquire lock to prevent concurrent fetches
        if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            _logger.LogWarning("Application token fetch timed out waiting for lock");
            return;
        }

        try
        {
            // Double-check cache after acquiring lock
            if (_dynamicIdToPackageName.TryGetValue(targetServiceId, out var mappedPackageName) &&
                _dynamicTokenCache.TryGetValue(mappedPackageName, out var existing) && !existing.IsExpired)
            {
                _logger.LogDebug("Application token already refreshed by another thread");
                return;
            }

            var request = new LinbikApplicationTokenRequest
            {
                SourceServiceId = Guid.Parse(_options.ServiceId),
                TargetServiceIds = [targetServiceId]
            };

            _logger.LogDebug("Fetching dynamic application token for service {ServiceId}", targetServiceId);

            var response = await _authClient.GetApplicationTokensAsync(request, cancellationToken);

            var integration = response?.Integrations?.FirstOrDefault();
            if (integration == null)
            {
                _logger.LogWarning("Application token response was null or empty for service {ServiceId}", targetServiceId);
                return;
            }

            var now = DateTime.UtcNow;
            // AccessTokenExpiresAt is Unix timestamp (seconds since epoch)
            var expiry = response!.AccessTokenExpiresAt > 0
                ? DateTimeOffset.FromUnixTimeSeconds(response.AccessTokenExpiresAt).UtcDateTime
                : now.AddMinutes(_options.AppsTokenLifetimeMinutes);

            var cacheItem = new ApplicationTokenCacheItem
            {
                Integration = integration,
                ExpiresAt = expiry,
                FetchedAt = now
            };

            // The server identifies the token only by package name, so remember which
            // service ID it corresponds to for future cache lookups by ID.
            _dynamicIdToPackageName[targetServiceId] = integration.PackageName;
            _dynamicTokenCache.AddOrUpdate(integration.PackageName, cacheItem, (_, _) => cacheItem);

            _logger.LogInformation("Cached dynamic application token for service {ServiceId} ({Package}), expires at {Expiry}",
                targetServiceId, integration.PackageName, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch dynamic application token for service {ServiceId}", targetServiceId);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task AutoRefreshTokensAsync()
    {
        try
        {
            if (_tokenCache.IsEmpty)
                return;

            // Check if any config-based tokens need refresh (not dynamic - those are on-demand)
            var needsRefresh = _tokenCache.Values.Any(c => c.NeedsRefresh(_options.AppsRefreshThreshold));

            if (needsRefresh)
            {
                _logger.LogDebug("Auto-refreshing config-based application tokens");
                await RefreshApplicationTokensAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application token auto-refresh failed");
        }
    }

    /// <summary>
    /// In Keyless Mode, <see cref="LinbikOptions.ServiceId"/> is only populated once provisioning
    /// completes. Provisioning normally happens lazily on the app's first OAuth login, which never
    /// runs for pure application/webhook workloads. Calling this before any apps token request guarantees
    /// ServiceId/ApiKey are available even if the app never processes a user login.
    /// Cheap no-op once already provisioned or when not running in Keyless Mode.
    /// </summary>
    private async Task EnsureKeylessProvisionedAsync(CancellationToken cancellationToken)
    {
        if (!_options.KeylessMode || !string.IsNullOrEmpty(_options.ServiceId))
            return;

        try
        {
            await _provisionClient.EnsureProvisionedAsync("http://localhost", "/api/linbik/callback", clientName: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keyless Mode auto-provisioning failed before apps token request");
        }
    }

    #endregion

    public void Dispose()
    {
        _autoRefreshTimer?.Dispose();
        _refreshLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
