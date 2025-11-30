using System.Net.Http.Json;
using System.Text.Json;
using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Core.Services;

/// <summary>
/// HTTP client implementation for Linbik authorization server communication
/// Uses HttpClientFactory with typed client pattern
/// </summary>
public class LinbikAuthClient : ILinbikAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly LinbikOptions _options;
    private readonly ILogger<LinbikAuthClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LinbikAuthClient(
        HttpClient httpClient,
        IOptions<LinbikOptions> options,
        ILogger<LinbikAuthClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configure HttpClient base address from LinbikOptions
        if (!string.IsNullOrEmpty(_options.LinbikUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.LinbikUrl.TrimEnd('/') + "/");
        }
    }

    /// <inheritdoc />
    public async Task<LinbikTokenResponse?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("ExchangeCodeAsync called with empty code");
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint.TrimStart('/'))
            {
                Content = JsonContent.Create(new LinbikTokenRequest
                {
                    ServiceId = Guid.Parse(_options.ServiceId)
                }, options: JsonOptions)
            };

            // Add required headers
            request.Headers.Add("Code", code);
            request.Headers.Add("ApiKey", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Token exchange failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);

                // Try to deserialize error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LinbikErrorResponse>(errorContent, JsonOptions);
                    _logger.LogWarning("Token exchange error: {Error} - {Description}",
                        errorResponse?.Error, errorResponse?.ErrorDescription);
                }
                catch
                {
                    // Ignore deserialization errors for error response
                }

                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<LinbikTokenResponse>(JsonOptions, cancellationToken);
            return tokenResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during token exchange");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed during token exchange");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token exchange");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<LinbikTokenResponse?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("RefreshTokensAsync called with empty refresh token");
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _options.RefreshEndpoint.TrimStart('/'))
            {
                Content = JsonContent.Create(new LinbikTokenRequest
                {
                    ServiceId = Guid.Parse(_options.ServiceId)
                }, options: JsonOptions)
            };

            // Add required headers
            request.Headers.Add("RefreshToken", refreshToken);
            request.Headers.Add("ApiKey", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Token refresh failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);

                // Try to deserialize error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LinbikErrorResponse>(errorContent, JsonOptions);
                    _logger.LogWarning("Token refresh error: {Error} - {Description}",
                        errorResponse?.Error, errorResponse?.ErrorDescription);
                }
                catch
                {
                    // Ignore deserialization errors for error response
                }

                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<LinbikTokenResponse>(JsonOptions, cancellationToken);
            return tokenResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during token refresh");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed during token refresh");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            throw;
        }
    }
}
