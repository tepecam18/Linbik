using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Linbik.Core.Services;

/// <summary>
/// HTTP client implementation for Linbik authorization server communication
/// Uses HttpClientFactory with typed client pattern
/// </summary>
public sealed class LinbikAuthClient(
    HttpClient httpClient,
    IOptions<LinbikOptions> options,
    ILogger<LinbikAuthClient> logger) : ILinbikAuthClient
{
    private readonly HttpClient _httpClient = SetupBaseAddress(httpClient, options.Value);
    private readonly LinbikOptions _options = options.Value;
    private readonly ILogger<LinbikAuthClient> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>HttpClient'ın BaseAddress'ini LinbikOptions'a göre yapılandırır.</summary>
    private static HttpClient SetupBaseAddress(HttpClient client, LinbikOptions opts)
    {
        if (!string.IsNullOrEmpty(opts.LinbikUrl))
            client.BaseAddress = new Uri(opts.LinbikUrl.TrimEnd('/') + "/");
        return client;
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

    #region S2S (Service-to-Service) Operations

    /// <inheritdoc />
    public async Task<LinbikS2STokenResponse?> GetS2STokensAsync(
        LinbikS2STokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceServiceId == Guid.Empty)
        {
            _logger.LogWarning("GetS2STokensAsync called with empty source service ID");
            return null;
        }

        if (request.TargetServiceIds == null || request.TargetServiceIds.Count == 0)
        {
            _logger.LogWarning("GetS2STokensAsync called with empty target service IDs");
            return null;
        }

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.S2STokenEndpoint.TrimStart('/'))
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            // Add API key header
            httpRequest.Headers.Add("ApiKey", _options.ApiKey);

            _logger.LogDebug("Requesting S2S tokens for {TargetCount} services from {SourceServiceId}",
                request.TargetServiceIds.Count, request.SourceServiceId);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("S2S token request failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);

                // Try to deserialize error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LinbikErrorResponse>(errorContent, JsonOptions);
                    _logger.LogWarning("S2S token error: {Error} - {Description}",
                        errorResponse?.Error, errorResponse?.ErrorDescription);
                }
                catch
                {
                    // Ignore deserialization errors for error response
                }

                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<LinbikS2STokenResponse>(JsonOptions, cancellationToken);

            _logger.LogInformation("Successfully obtained S2S tokens for {IntegrationCount} services",
                tokenResponse?.Integrations?.Count ?? 0);

            return tokenResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during S2S token request");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed during S2S token request");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during S2S token request");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<LinbikS2STokenResponse?> GetS2STokensAsync(
        IEnumerable<string> targetPackageNames,
        CancellationToken cancellationToken = default)
    {
        // Check if target services are configured in options
        if (_options.S2STargetServices == null || _options.S2STargetServices.Count == 0)
        {
            _logger.LogError("No S2S target services configured. Add Linbik:S2STargetServices configuration.");
            return null;
        }

        List<Guid> targetIds = [];
        foreach (var packageName in targetPackageNames)
        {
            if (_options.S2STargetServices.TryGetValue(packageName, out var serviceId))
            {
                targetIds.Add(serviceId);
            }
            else
            {
                _logger.LogWarning("Target service {PackageName} not found in S2STargetServices configuration", packageName);
            }
        }

        if (targetIds.Count == 0)
        {
            _logger.LogError("No matching target services found for requested package names");
            return null;
        }

        var request = new LinbikS2STokenRequest
        {
            SourceServiceId = Guid.Parse(_options.ServiceId),
            TargetServiceIds = targetIds
        };

        return await GetS2STokensAsync(request, cancellationToken);
    }

    #endregion
}
