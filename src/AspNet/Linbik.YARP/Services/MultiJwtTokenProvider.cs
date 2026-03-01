using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Linbik.YARP.Interfaces;
using System.Collections.Concurrent;

namespace Linbik.YARP.Services;

/// <summary>
/// Token provider for multi-service authentication with caching
/// </summary>
public sealed class MultiJwtTokenProvider(ILinbikAuthClient linbikAuthClient) : ITokenProvider
{
    private const int TokenExpiryBufferMinutes = 5;

    private sealed class TokenCacheItem
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
    }

    private sealed class MultiServiceTokenCache
    {
        public LinbikTokenResponse Response { get; set; } = null!;
        public DateTime RefreshTokenExpiry { get; set; }
        public ConcurrentDictionary<string, TokenCacheItem> IntegrationTokens { get; set; } = new();
    }

    private MultiServiceTokenCache? _multiServiceCache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Gets multi-service token response from authorization code
    /// Uses LinbikAuthClient from Linbik.Core for proper HTTP communication
    /// </summary>
    public async Task<LinbikTokenResponse?> GetMultiServiceTokenAsync(string baseUrl, string authorizationCode, string apiKey)
    {
        if (string.IsNullOrEmpty(authorizationCode))
            throw new ArgumentException("Authorization code cannot be null or empty", nameof(authorizationCode));

        await _lock.WaitAsync();
        try
        {
            // Use LinbikAuthClient instead of manual HTTP call
            var tokenResponse = await linbikAuthClient.ExchangeCodeAsync(authorizationCode);

            if (tokenResponse != null)
            {
                CacheTokenResponse(tokenResponse);
            }

            return tokenResponse;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Refreshes all service tokens using refresh token
    /// Uses LinbikAuthClient from Linbik.Core for proper HTTP communication
    /// </summary>
    public async Task<LinbikTokenResponse?> RefreshTokensAsync(string baseUrl, string refreshToken, string apiKey, string serviceId)
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw new ArgumentException("Refresh token cannot be null or empty", nameof(refreshToken));

        await _lock.WaitAsync();
        try
        {
            // Use LinbikAuthClient instead of manual HTTP call
            var tokenResponse = await linbikAuthClient.RefreshTokensAsync(refreshToken);

            if (tokenResponse != null)
            {
                CacheTokenResponse(tokenResponse);
            }

            return tokenResponse;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets a specific integration service token from cache
    /// </summary>
    public Task<string?> GetIntegrationTokenAsync(string integrationServicePackage)
    {
        if (string.IsNullOrEmpty(integrationServicePackage))
            throw new ArgumentException("Integration service package cannot be null or empty", nameof(integrationServicePackage));

        if (_multiServiceCache == null)
            return Task.FromResult<string?>(null);

        // Check if token is cached and valid
        if (_multiServiceCache.IntegrationTokens.TryGetValue(integrationServicePackage, out var cached) &&
            DateTime.UtcNow < cached.Expiry)
        {
            return Task.FromResult<string?>(cached.Token);
        }

        // Token expired or not found
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Stores multi-service token response in cache
    /// </summary>
    public void CacheTokenResponse(LinbikTokenResponse tokenResponse)
    {
        if (tokenResponse == null)
            throw new ArgumentNullException(nameof(tokenResponse));

        var cache = new MultiServiceTokenCache
        {
            Response = tokenResponse,
            RefreshTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt ?? 0).UtcDateTime,
            IntegrationTokens = new ConcurrentDictionary<string, TokenCacheItem>()
        };

        // Cache each integration token
        if (tokenResponse.Integrations != null)
        {
            foreach (var integration in tokenResponse.Integrations)
            {
                cache.IntegrationTokens[integration.PackageName] = new TokenCacheItem
                {
                    Token = integration.Token,
                    // Token expiry is 1 hour (from Linbik.App documentation)
                    Expiry = DateTime.UtcNow.AddHours(1).AddMinutes(-TokenExpiryBufferMinutes)
                };
            }
        }

        _multiServiceCache = cache;
    }

    /// <summary>
    /// Clears all cached tokens
    /// </summary>
    public void ClearCache()
    {
        _multiServiceCache = null;
    }
}
