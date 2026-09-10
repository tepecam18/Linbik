using System.Net.Http.Json;
using System.Text.Json;
using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Responses;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    public async Task<LBaseResponse<LinbikInitiateResponse>> InitiateAuthAsync(LinbikInitiateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ClientId == Guid.Empty)
        {
            _logger.LogWarning("InitiateAuthAsync called with empty client ID");
            return new LBaseResponse<LinbikInitiateResponse>("invalid_request", "Client ID is required.");
        }

        try
        {
            using var httpRequest = CreateRequest(HttpMethod.Post, _options.AuthorizationEndpoint.TrimStart('/'), request);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auth initiate failed with status {StatusCode}: {Error}",
                    response.StatusCode, body);
            }

            // Deserialize as LBaseResponse wrapper (API always returns BaseResponse<T>)
            var result = JsonSerializer.Deserialize<LBaseResponse<LinbikInitiateResponse>>(body, JsonOptions);
            return result ?? new LBaseResponse<LinbikInitiateResponse>("deserialization_error", "Failed to parse server response.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during auth initiate");
            return new LBaseResponse<LinbikInitiateResponse>("connection_error", $"Could not reach Linbik server: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed during auth initiate");
            return new LBaseResponse<LinbikInitiateResponse>("deserialization_error", $"Invalid response format: {ex.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during auth initiate");
            throw;
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
            using var request = CreateRequest(HttpMethod.Post, _options.TokenEndpoint.TrimStart('/'), new LinbikTokenRequest
            {
                ServiceId = Guid.Parse(_options.ServiceId)
            });

            // Add required headers
            request.Headers.Add("Code", code);

            using var responseMessage = await _httpClient.SendAsync(request, cancellationToken);

            var responseBody = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
            var response = JsonSerializer.Deserialize<LBaseResponse<LinbikTokenResponse>>(responseBody, JsonOptions);
            if (!responseMessage.IsSuccessStatusCode || !(response?.IsSuccess ?? false))
            {
                _logger.LogWarning("Token exchange failed with status {StatusCode}: {Error}",
                    responseMessage.StatusCode, responseBody);

                // Try to deserialize error response
                try
                {
                    _logger.LogWarning("Token exchange error: {Error} - {Description}",
                        response?.FriendlyMessage?.Title, response?.FriendlyMessage?.Message);
                }
                catch
                {
                    // Ignore deserialization errors for error response
                }

                return null;
            }

            return response?.Data;
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
            using var request = CreateRequest(HttpMethod.Post, _options.RefreshEndpoint.TrimStart('/'), new LinbikTokenRequest
            {
                ServiceId = Guid.Parse(_options.ServiceId)
            });

            // Add required headers
            request.Headers.Add("RefreshToken", refreshToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

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

            var result = await response.Content.ReadFromJsonAsync<LBaseResponse<LinbikTokenResponse>>(JsonOptions, cancellationToken);
            if (result?.IsSuccess != true || result.Data is not { } tokenResponse
                || tokenResponse.UserId == Guid.Empty || string.IsNullOrWhiteSpace(tokenResponse.Username))
            {
                _logger.LogWarning("Token refresh returned an unsuccessful or incomplete response");
                return null;
            }
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            throw;
        }
    }

    #region Apps (Service-to-Service) Operations

    /// <inheritdoc />
    public async Task<LinbikApplicationTokenResponse?> GetApplicationTokensAsync(
        LinbikApplicationTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceServiceId == Guid.Empty)
        {
            _logger.LogWarning("GetApplicationTokensAsync called with empty source service ID");
            return null;
        }

        var hasTargetIds = request.TargetServiceIds is { Count: > 0 };
        var hasTargetPackageNames = request.TargetPackageNames is { Count: > 0 };
        if (!hasTargetIds && !hasTargetPackageNames)
        {
            _logger.LogWarning("GetApplicationTokensAsync called with no target service IDs or package names");
            return null;
        }

        try
        {
            using var httpRequest = CreateRequest(HttpMethod.Post, _options.AppsTokenEndpoint.TrimStart('/'), request);

            _logger.LogDebug("Requesting apps tokens for {TargetCount} services from {SourceServiceId}",
                (request.TargetServiceIds?.Count ?? 0) + (request.TargetPackageNames?.Count ?? 0), request.SourceServiceId);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Apps token request failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);

                // Try to deserialize error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<LinbikErrorResponse>(errorContent, JsonOptions);
                    _logger.LogWarning("Apps token error: {Error} - {Description}",
                        errorResponse?.Error, errorResponse?.ErrorDescription);
                }
                catch
                {
                    // Ignore deserialization errors for error response
                }

                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<LBaseResponse<LinbikApplicationTokenResponse>>(JsonOptions, cancellationToken);

            _logger.LogInformation("Successfully obtained apps tokens for {IntegrationCount} services",
                tokenResponse?.Data?.Integrations?.Count ?? 0);

            return tokenResponse?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during apps token request");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed during apps token request");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during apps token request");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<LinbikApplicationTokenResponse?> GetApplicationTokensAsync(
        IEnumerable<string> targetPackageNames,
        CancellationToken cancellationToken = default)
    {
        var packageNames = targetPackageNames.Distinct().ToList();
        if (packageNames.Count == 0)
        {
            _logger.LogError("No target package names provided");
            return null;
        }

        var request = new LinbikApplicationTokenRequest
        {
            SourceServiceId = Guid.Parse(_options.ServiceId),
            TargetPackageNames = packageNames
        };

        return await GetApplicationTokensAsync(request, cancellationToken);
    }

    #endregion

    #region Client Management

    /// <inheritdoc />
    public async Task<bool> UpdateClientRedirectUriByNameAsync(string clientName, string redirectUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(redirectUri))
        {
            _logger.LogWarning("UpdateClientRedirectUriByNameAsync called with empty clientName or redirectUri");
            return false;
        }

        try
        {
            var endpoint = $"api/services/{_options.ServiceId}/clients/by-name";

            using var request = CreateRequest(HttpMethod.Put, endpoint, new { name = clientName, redirectUri });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Client RedirectUri update failed for '{ClientName}' with status {StatusCode}: {Error}",
                    clientName, response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Successfully updated RedirectUri for client '{ClientName}' to '{RedirectUri}'",
                clientName, redirectUri);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during client RedirectUri update for '{ClientName}'", clientName);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during client RedirectUri update for '{ClientName}'", clientName);
            return false;
        }
    }

    #endregion

    #region Private Helpers

    private HttpRequestMessage CreateRequest<T>(HttpMethod method, string endpoint, T body)
    {
        var request = new HttpRequestMessage(method, endpoint)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Add("ApiKey", _options.ApiKey);
        AddDiagnosticHeaders(request);
        return request;
    }

    private static void AddDiagnosticHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderMode, "sdk");
        request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderPlatform, "aspnet");

        var version = typeof(LinbikAuthClient).Assembly.GetName().Version;
        if (version is not null)
            request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderVersion, version.ToString(3));
    }

    #endregion
}
