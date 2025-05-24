using Linbik.YARP.Interfaces;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Linbik.YARP;

internal class MultiJwtTokenProvider : ITokenProvider
{
    private class TokenCacheItem
    {
        public string Token { get; set; }
        public DateTime Expiry { get; set; }
    }

    private readonly ConcurrentDictionary<string, TokenCacheItem> _tokenCache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;

    public MultiJwtTokenProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
        if (_tokenCache.TryGetValue(baseUrl, out var cached) && DateTime.UtcNow < cached.Expiry)
        {
            return cached.Token;
        }

        await _lock.WaitAsync();
        try
        {
            if (_tokenCache.TryGetValue(baseUrl, out cached) && DateTime.UtcNow < cached.Expiry)
            {
                return cached.Token;
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                $"{baseUrl}/linbik/app-login",
                new StringContent(JsonSerializer.Serialize(new
                {
                    client_id = clientId,
                    client_secret = clientSecret
                }), Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonSerializer.Deserialize<TokenResponse>(json);

            var expiry = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn - 60);
            _tokenCache[baseUrl] = new TokenCacheItem
            {
                Token = tokenResult.AccessToken,
                Expiry = expiry
            };

            return tokenResult.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private class TokenResponse
    {
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}
