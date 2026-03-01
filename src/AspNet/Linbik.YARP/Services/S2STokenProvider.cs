using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Linbik.YARP.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Linbik.YARP.Services;

/// <summary>
/// S2S (Service-to-Service) token provider with automatic caching and refresh
/// Uses Linbik.Core's ILinbikAuthClient for HTTP operations
/// Supports both config-based (package name) and dynamic (service ID) targets
/// </summary>
public sealed class S2STokenProvider : IS2STokenProvider, IDisposable
{
    private readonly ILinbikAuthClient _authClient;
    private readonly LinbikOptions _options;
    private readonly ILogger<S2STokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Timer? _autoRefreshTimer;

    // Cache for S2S tokens - by package name (config-based)
    private readonly ConcurrentDictionary<string, S2STokenCacheItem> _tokenCache = new();
    // Cache for S2S tokens - by service ID (dynamic)
    private readonly ConcurrentDictionary<Guid, S2STokenCacheItem> _dynamicTokenCache = new();
    private DateTime _cacheExpiry = DateTime.MinValue;

    private sealed class S2STokenCacheItem
    {
        public required LinbikS2SIntegration Integration { get; init; }
        public DateTime ExpiresAt { get; init; }
        public DateTime FetchedAt { get; init; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool NeedsRefresh(double threshold) =>
            DateTime.UtcNow >= FetchedAt.Add(TimeSpan.FromTicks((long)((ExpiresAt - FetchedAt).Ticks * threshold)));
    }

    public S2STokenProvider(
        ILinbikAuthClient authClient,
        IOptions<LinbikOptions> options,
        ILogger<S2STokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(authClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _authClient = authClient;
        _options = options.Value;
        _logger = logger;

        // Setup auto-refresh timer if enabled (only for config-based services)
        if (_options.S2SAutoRefresh && _options.S2STargetServices.Count > 0)
        {
            var refreshInterval = TimeSpan.FromMinutes(_options.S2STokenLifetimeMinutes * _options.S2SRefreshThreshold);
            _autoRefreshTimer = new Timer(
                async _ => await AutoRefreshTokensAsync(),
                null,
                refreshInterval,
                refreshInterval);

            _logger.LogInformation("S2S auto-refresh enabled with interval: {Interval}", refreshInterval);
        }
    }

    #region Package Name Based (Config-based targets)

    /// <inheritdoc />
    public async Task<string?> GetS2STokenAsync(string integrationPackageName, CancellationToken cancellationToken = default)
    {
        var integration = await GetS2SIntegrationAsync(integrationPackageName, cancellationToken);
        return integration?.Token;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetS2STokensAsync(
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
    public async Task<LinbikS2SIntegration?> GetS2SIntegrationAsync(
        string integrationPackageName,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_tokenCache.TryGetValue(integrationPackageName, out var cached))
        {
            if (!cached.IsExpired)
            {
                // Check if needs proactive refresh
                if (cached.NeedsRefresh(_options.S2SRefreshThreshold))
                {
                    _logger.LogDebug("S2S token for {Package} needs refresh (threshold: {Threshold}%)",
                        integrationPackageName, _options.S2SRefreshThreshold * 100);

                    // Trigger background refresh but return current token
                    _ = Task.Run(async () => await RefreshS2STokensAsync(cancellationToken), cancellationToken);
                }

                return cached.Integration;
            }
            else
            {
                _logger.LogDebug("S2S token for {Package} expired", integrationPackageName);
            }
        }

        // Need to fetch token
        await FetchAndCacheTokensByPackageAsync([integrationPackageName], cancellationToken);

        if (_tokenCache.TryGetValue(integrationPackageName, out cached))
        {
            return cached.Integration;
        }

        _logger.LogWarning("Failed to obtain S2S token for {Package}", integrationPackageName);
        return null;
    }

    #endregion

    #region Service ID Based (Dynamic targets)

    /// <inheritdoc />
    public async Task<LinbikS2SIntegration?> GetS2SIntegrationByIdAsync(
        Guid targetServiceId,
        CancellationToken cancellationToken = default)
    {
        // Check dynamic cache first
        if (_dynamicTokenCache.TryGetValue(targetServiceId, out var cached))
        {
            if (!cached.IsExpired)
            {
                // Check if needs proactive refresh
                if (cached.NeedsRefresh(_options.S2SRefreshThreshold))
                {
                    _logger.LogDebug("Dynamic S2S token for {ServiceId} needs refresh", targetServiceId);

                    // Trigger background refresh but return current token
                    _ = Task.Run(async () => await FetchAndCacheDynamicTokensAsync([targetServiceId], default), cancellationToken);
                }

                return cached.Integration;
            }
            else
            {
                _logger.LogDebug("Dynamic S2S token for {ServiceId} expired", targetServiceId);
            }
        }

        // Need to fetch token
        await FetchAndCacheDynamicTokensAsync([targetServiceId], cancellationToken);

        if (_dynamicTokenCache.TryGetValue(targetServiceId, out cached))
        {
            return cached.Integration;
        }

        _logger.LogWarning("Failed to obtain dynamic S2S token for service {ServiceId}", targetServiceId);
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LinbikS2SIntegration>> GetS2SIntegrationsByIdAsync(
        IEnumerable<Guid> targetServiceIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, LinbikS2SIntegration>();
        var serviceIds = targetServiceIds.ToList();

        // Check which tokens we need to fetch
        var needFetch = new List<Guid>();
        foreach (var serviceId in serviceIds)
        {
            if (_dynamicTokenCache.TryGetValue(serviceId, out var cached) && !cached.IsExpired)
            {
                result[serviceId] = cached.Integration;
            }
            else
            {
                needFetch.Add(serviceId);
            }
        }

        // Fetch missing tokens
        if (needFetch.Count > 0)
        {
            await FetchAndCacheDynamicTokensAsync(needFetch, cancellationToken);

            // Add newly fetched tokens to result
            foreach (var serviceId in needFetch)
            {
                if (_dynamicTokenCache.TryGetValue(serviceId, out var cached))
                {
                    result[serviceId] = cached.Integration;
                }
            }
        }

        return result;
    }

    #endregion

    #region Cache Management

    /// <inheritdoc />
    public async Task RefreshS2STokensAsync(CancellationToken cancellationToken = default)
    {
        var packageNames = _options.S2STargetServices.Keys.ToList();
        if (packageNames.Count == 0)
        {
            _logger.LogWarning("No S2S target services configured");
            return;
        }

        await FetchAndCacheTokensByPackageAsync(packageNames, cancellationToken);
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _tokenCache.Clear();
        _dynamicTokenCache.Clear();
        _cacheExpiry = DateTime.MinValue;
        _logger.LogInformation("S2S token cache cleared (both config-based and dynamic)");
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
        // Get target service IDs from options
        var targetIds = new List<Guid>();
        foreach (var packageName in packageNames)
        {
            if (_options.S2STargetServices.TryGetValue(packageName, out var serviceId))
            {
                targetIds.Add(serviceId);
            }
            else
            {
                _logger.LogWarning("S2S target service {Package} not found in configuration", packageName);
            }
        }

        if (targetIds.Count == 0)
        {
            _logger.LogError("No valid S2S target service IDs found");
            return;
        }

        await FetchAndCacheTokensCoreAsync(targetIds, isConfigBased: true, cancellationToken);
    }

    private async Task FetchAndCacheDynamicTokensAsync(
        IEnumerable<Guid> targetServiceIds,
        CancellationToken cancellationToken)
    {
        var targetIds = targetServiceIds.ToList();
        if (targetIds.Count == 0)
            return;

        await FetchAndCacheTokensCoreAsync(targetIds, isConfigBased: false, cancellationToken);
    }

    private async Task FetchAndCacheTokensCoreAsync(
        List<Guid> targetIds,
        bool isConfigBased,
        CancellationToken cancellationToken)
    {
        // Acquire lock to prevent concurrent fetches
        if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            _logger.LogWarning("S2S token fetch timed out waiting for lock");
            return;
        }

        try
        {
            // Double-check cache after acquiring lock
            var stillNeedFetch = new List<Guid>();
            foreach (var id in targetIds)
            {
                if (isConfigBased)
                {
                    // For config-based, check by package name mapping
                    var packageName = _options.S2STargetServices.FirstOrDefault(x => x.Value == id).Key;
                    if (string.IsNullOrEmpty(packageName) || !_tokenCache.TryGetValue(packageName, out var c) || c.IsExpired)
                    {
                        stillNeedFetch.Add(id);
                    }
                }
                else
                {
                    // For dynamic, check directly by ID
                    if (!_dynamicTokenCache.TryGetValue(id, out var c) || c.IsExpired)
                    {
                        stillNeedFetch.Add(id);
                    }
                }
            }

            if (stillNeedFetch.Count == 0)
            {
                _logger.LogDebug("S2S tokens already refreshed by another thread");
                return;
            }

            var request = new LinbikS2STokenRequest
            {
                SourceServiceId = Guid.Parse(_options.ServiceId),
                TargetServiceIds = stillNeedFetch
            };

            var cacheType = isConfigBased ? "config-based" : "dynamic";
            _logger.LogDebug("Fetching {CacheType} S2S tokens for {Count} services", cacheType, stillNeedFetch.Count);

            var response = await _authClient.GetS2STokensAsync(request, cancellationToken);

            if (response?.Integrations == null)
            {
                _logger.LogWarning("S2S token response was null or empty");
                return;
            }

            var now = DateTime.UtcNow;
            // AccessTokenExpiresAt is Unix timestamp (seconds since epoch)
            var expiry = response.AccessTokenExpiresAt > 0
                ? DateTimeOffset.FromUnixTimeSeconds(response.AccessTokenExpiresAt).UtcDateTime
                : now.AddMinutes(_options.S2STokenLifetimeMinutes);

            foreach (var integration in response.Integrations)
            {
                var cacheItem = new S2STokenCacheItem
                {
                    Integration = integration,
                    ExpiresAt = expiry,
                    FetchedAt = now
                };

                if (isConfigBased)
                {
                    // Cache by package name
                    _tokenCache.AddOrUpdate(integration.PackageName, cacheItem, (_, _) => cacheItem);
                }
                else
                {
                    // Cache by service ID
                    _dynamicTokenCache.AddOrUpdate(integration.ServiceId, cacheItem, (_, _) => cacheItem);
                }
            }

            if (isConfigBased)
            {
                _cacheExpiry = expiry;
            }

            _logger.LogInformation("Cached {CacheType} S2S tokens for {Count} services, expires at {Expiry}",
                cacheType, response.Integrations.Count, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch S2S tokens");
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
            var needsRefresh = _tokenCache.Values.Any(c => c.NeedsRefresh(_options.S2SRefreshThreshold));

            if (needsRefresh)
            {
                _logger.LogDebug("Auto-refreshing config-based S2S tokens");
                await RefreshS2STokensAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S2S auto-refresh failed");
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
