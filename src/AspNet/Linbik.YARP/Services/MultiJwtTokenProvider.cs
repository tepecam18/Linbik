using Linbik.Core.Models;
using Linbik.YARP.Interfaces;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Linbik.YARP.Services;

/// <summary>
/// Token provider for multi-service authentication with caching
/// </summary>
public class MultiJwtTokenProvider : ITokenProvider
{
    private const string TokenExchangeEndpoint = "/auth/token";
    private const string RefreshTokenEndpoint = "/auth/refresh";
    private const string JsonContentType = "application/json";
    private const int TokenExpiryBufferMinutes = 5;

    private class TokenCacheItem
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
    }

    private class MultiServiceTokenCache
    {
        public MultiServiceTokenResponse Response { get; set; } = null!;
        public DateTime RefreshTokenExpiry { get; set; }
        public ConcurrentDictionary<string, TokenCacheItem> IntegrationTokens { get; set; } = new();
    }

    private MultiServiceTokenCache? _multiServiceCache;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;

    public MultiJwtTokenProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Gets multi-service token response from authorization code
    /// </summary>
    public async Task<MultiServiceTokenResponse?> GetMultiServiceTokenAsync(string baseUrl, string authorizationCode, string apiKey)
    {
        if (string.IsNullOrEmpty(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        if (string.IsNullOrEmpty(authorizationCode))
            throw new ArgumentException("Authorization code cannot be null or empty", nameof(authorizationCode));

        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        await _lock.WaitAsync();
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{TokenExchangeEndpoint}");
            request.Headers.Add("ApiKey", apiKey);
            request.Headers.Add("Code", authorizationCode);
            request.Content = new StringContent("{}", Encoding.UTF8, JsonContentType);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<MultiServiceTokenResponse>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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
    /// </summary>
    public async Task<MultiServiceTokenResponse?> RefreshTokensAsync(string baseUrl, string refreshToken, string apiKey, string serviceId)
    {
        if (string.IsNullOrEmpty(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        if (string.IsNullOrEmpty(refreshToken))
            throw new ArgumentException("Refresh token cannot be null or empty", nameof(refreshToken));

        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        if (string.IsNullOrEmpty(serviceId))
            throw new ArgumentException("Service ID cannot be null or empty", nameof(serviceId));

        await _lock.WaitAsync();
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{RefreshTokenEndpoint}");
            request.Headers.Add("ApiKey", apiKey);
            request.Headers.Add("RefreshToken", refreshToken);
            request.Content = new StringContent(JsonSerializer.Serialize(new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                ApiKey = apiKey,
                CommunityId = serviceId
            }), Encoding.UTF8, JsonContentType);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<MultiServiceTokenResponse>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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
    public void CacheTokenResponse(MultiServiceTokenResponse tokenResponse)
    {
        if (tokenResponse == null)
            throw new ArgumentNullException(nameof(tokenResponse));

        var cache = new MultiServiceTokenCache
        {
            Response = tokenResponse,
            RefreshTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt).UtcDateTime,
            IntegrationTokens = new ConcurrentDictionary<string, TokenCacheItem>()
        };

        // Cache each integration token
        foreach (var integration in tokenResponse.Integrations)
        {
            cache.IntegrationTokens[integration.ServicePackage] = new TokenCacheItem
            {
                Token = integration.Token,
                Expiry = integration.ExpiresAt.AddMinutes(-TokenExpiryBufferMinutes)
            };
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
