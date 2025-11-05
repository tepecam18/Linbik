using Linbik.Core.Models;
using Linbik.Core.Responses;
using Linbik.YARP.Interfaces;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Linbik.YARP.Services;

public class MultiJwtTokenProvider : ITokenProvider
{
    private const string LegacyAppLoginEndpoint = "/linbik/app-login";
    private const string TokenExchangeEndpoint = "/oauth/token";
    private const string RefreshTokenEndpoint = "/oauth/refresh";
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

    private readonly ConcurrentDictionary<string, TokenCacheItem> _legacyTokenCache = new();
    private MultiServiceTokenCache? _multiServiceCache;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;

    public MultiJwtTokenProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Legacy: Gets a single JWT token (deprecated)
    /// </summary>
    [Obsolete("Use GetMultiServiceTokenAsync for OAuth 2.0 flow")]
    public async Task<string> GetTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
        if (string.IsNullOrEmpty(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        if (string.IsNullOrEmpty(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        if (string.IsNullOrEmpty(clientSecret))
            throw new ArgumentException("Client secret cannot be null or empty", nameof(clientSecret));

        if (_legacyTokenCache.TryGetValue(baseUrl, out var cached) && DateTime.UtcNow < cached.Expiry)
        {
            return cached.Token;
        }

        await _lock.WaitAsync();
        try
        {
            if (_legacyTokenCache.TryGetValue(baseUrl, out cached) && DateTime.UtcNow < cached.Expiry)
            {
                return cached.Token;
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                $"{baseUrl}{LegacyAppLoginEndpoint}",
                new StringContent(JsonSerializer.Serialize(new
                {
                    client_id = clientId,
                    client_secret = clientSecret
                }), Encoding.UTF8, JsonContentType)
            );

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonSerializer.Deserialize<LBaseResponse<TokenResponse>>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (tokenResult?.Data == null)
                throw new InvalidOperationException("Failed to deserialize token response");

            var expiry = DateTime.UtcNow.AddMinutes(tokenResult.Data.ExpiresIn - TokenExpiryBufferMinutes);
            _legacyTokenCache[baseUrl] = new TokenCacheItem
            {
                Token = tokenResult.Data.Token,
                Expiry = expiry
            };

            return tokenResult.Data.Token;
        }
        finally
        {
            _lock.Release();
        }
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
    /// Gets a specific integration service token from cache or refreshes if expired
    /// </summary>
    public async Task<string?> GetIntegrationTokenAsync(string integrationServicePackage)
    {
        if (string.IsNullOrEmpty(integrationServicePackage))
            throw new ArgumentException("Integration service package cannot be null or empty", nameof(integrationServicePackage));

        if (_multiServiceCache == null)
            return null;

        // Check if token is cached and valid
        if (_multiServiceCache.IntegrationTokens.TryGetValue(integrationServicePackage, out var cached) && 
            DateTime.UtcNow < cached.Expiry)
        {
            return cached.Token;
        }

        // Token expired or not found - need to refresh
        // Note: This requires the refresh token to be available
        // The calling code should handle refresh token logic
        return null;
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
        _legacyTokenCache.Clear();
        _multiServiceCache = null;
    }

    private class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
