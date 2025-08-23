using Linbik.Core.Responses;
using Linbik.YARP.Interfaces;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Linbik.YARP.Services;

public class MultiJwtTokenProvider : ITokenProvider
{
    private const string AppLoginEndpoint = "/linbik/app-login";
    private const string JsonContentType = "application/json";
    private const int TokenExpiryBufferMinutes = 1;

    private class TokenCacheItem
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
    }

    private readonly ConcurrentDictionary<string, TokenCacheItem> _tokenCache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;

    public MultiJwtTokenProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<string> GetTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
        if (string.IsNullOrEmpty(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

        if (string.IsNullOrEmpty(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        if (string.IsNullOrEmpty(clientSecret))
            throw new ArgumentException("Client secret cannot be null or empty", nameof(clientSecret));

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
                $"{baseUrl}{AppLoginEndpoint}",
                new StringContent(JsonSerializer.Serialize(new
                {
                    client_id = clientId,
                    client_secret = clientSecret
                }), Encoding.UTF8, JsonContentType)
            );

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonSerializer.Deserialize<LBaseResponse<TokenResponse>>(json);

            if (tokenResult?.Data == null)
                throw new InvalidOperationException("Failed to deserialize token response");

            var expiry = DateTime.UtcNow.AddMinutes(tokenResult.Data.ExpiresIn - TokenExpiryBufferMinutes);
            _tokenCache[baseUrl] = new TokenCacheItem
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

    private class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
