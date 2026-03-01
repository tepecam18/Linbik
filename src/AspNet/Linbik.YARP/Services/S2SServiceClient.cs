using Linbik.Core.Models;
using Linbik.Core.Responses;
using Linbik.YARP.Configuration;
using Linbik.YARP.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Linbik.YARP.Services;

/// <summary>
/// HTTP client for S2S (Service-to-Service) communication
/// Automatically injects S2S tokens and enforces LBaseResponse format
/// Supports both config-based (package name) and dynamic (service ID) targets
/// </summary>
public sealed class S2SServiceClient(
    HttpClient httpClient,
    IS2STokenProvider tokenProvider,
    IOptions<YARPOptions> options,
    ILogger<S2SServiceClient> logger) : IS2SServiceClient
{
    private readonly YARPOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region Package Name Based (Config-based targets)

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> GetAsync<TResponse>(
        string packageName,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        return await SendByPackageAsync<object, TResponse>(HttpMethod.Get, packageName, endpoint, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> PostAsync<TRequest, TResponse>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        return await SendByPackageAsync<TRequest, TResponse>(HttpMethod.Post, packageName, endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<object>> PostAsync<TRequest>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default) where TRequest : class
    {
        return await SendByPackageAsync<TRequest, object>(HttpMethod.Post, packageName, endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> PutAsync<TRequest, TResponse>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        return await SendByPackageAsync<TRequest, TResponse>(HttpMethod.Put, packageName, endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> DeleteAsync<TResponse>(
        string packageName,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        return await SendByPackageAsync<object, TResponse>(HttpMethod.Delete, packageName, endpoint, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<object>> DeleteAsync(
        string packageName,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        return await SendByPackageAsync<object, object>(HttpMethod.Delete, packageName, endpoint, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> PatchAsync<TRequest, TResponse>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        return await SendByPackageAsync<TRequest, TResponse>(HttpMethod.Patch, packageName, endpoint, request, cancellationToken);
    }

    #endregion

    #region Service ID Based (Dynamic targets)

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> GetByIdAsync<TResponse>(
        Guid targetServiceId,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        return await SendByIdAsync<object, TResponse>(HttpMethod.Get, targetServiceId, endpoint, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> PostByIdAsync<TRequest, TResponse>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        return await SendByIdAsync<TRequest, TResponse>(HttpMethod.Post, targetServiceId, endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<object>> PostByIdAsync<TRequest>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default) where TRequest : class
    {
        return await SendByIdAsync<TRequest, object>(HttpMethod.Post, targetServiceId, endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> PutByIdAsync<TRequest, TResponse>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        return await SendByIdAsync<TRequest, TResponse>(HttpMethod.Put, targetServiceId, endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> DeleteByIdAsync<TResponse>(
        Guid targetServiceId,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        return await SendByIdAsync<object, TResponse>(HttpMethod.Delete, targetServiceId, endpoint, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<object>> DeleteByIdAsync(
        Guid targetServiceId,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        return await SendByIdAsync<object, object>(HttpMethod.Delete, targetServiceId, endpoint, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LBaseResponse<TResponse>> PatchByIdAsync<TRequest, TResponse>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        return await SendByIdAsync<TRequest, TResponse>(HttpMethod.Patch, targetServiceId, endpoint, request, cancellationToken);
    }

    #endregion

    #region Private Methods - Package Name Based

    private async Task<LBaseResponse<TResponse>> SendByPackageAsync<TRequest, TResponse>(
        HttpMethod method,
        string packageName,
        string endpoint,
        TRequest? request,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResponse : class
    {
        try
        {
            // Get S2S token and integration details
            var integration = await tokenProvider.GetS2SIntegrationAsync(packageName, cancellationToken);
            if (integration == null)
            {
                logger.LogWarning("S2S token not available for {PackageName}", packageName);
                return new LBaseResponse<TResponse>("S2S Error", $"Token not available for {packageName}");
            }

            // Build target URL
            var baseUrl = GetBaseUrlByPackage(packageName, integration.ServiceUrl);
            return await SendCoreAsync<TRequest, TResponse>(method, baseUrl, endpoint, integration, request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleException<TResponse>(ex, $"package:{packageName}");
        }
    }

    #endregion

    #region Private Methods - Service ID Based

    private async Task<LBaseResponse<TResponse>> SendByIdAsync<TRequest, TResponse>(
        HttpMethod method,
        Guid targetServiceId,
        string endpoint,
        TRequest? request,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResponse : class
    {
        try
        {
            // Get S2S token and integration details by service ID (dynamic)
            var integration = await tokenProvider.GetS2SIntegrationByIdAsync(targetServiceId, cancellationToken);
            if (integration == null)
            {
                logger.LogWarning("S2S token not available for service ID {ServiceId}", targetServiceId);
                return new LBaseResponse<TResponse>("S2S Error", $"Token not available for service ID {targetServiceId}");
            }

            // ServiceUrl MUST be present for dynamic targets (fetched from Linbik)
            if (string.IsNullOrEmpty(integration.ServiceUrl))
            {
                logger.LogError("ServiceUrl not available for service ID {ServiceId}", targetServiceId);
                return new LBaseResponse<TResponse>("S2S Error", $"ServiceUrl not returned by Linbik for service ID {targetServiceId}");
            }

            var baseUrl = integration.ServiceUrl.TrimEnd('/');
            return await SendCoreAsync<TRequest, TResponse>(method, baseUrl, endpoint, integration, request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleException<TResponse>(ex, $"serviceId:{targetServiceId}");
        }
    }

    #endregion

    #region Private Methods - Core

    private async Task<LBaseResponse<TResponse>> SendCoreAsync<TRequest, TResponse>(
        HttpMethod method,
        string baseUrl,
        string endpoint,
        LinbikS2SIntegration integration,
        TRequest? request,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResponse : class
    {
        var targetUrl = BuildTargetUrl(baseUrl, endpoint);
        var targetDescription = $"{integration.PackageName ?? integration.ServiceId.ToString()}";

        logger.LogDebug("S2S {Method} request to {Target}: {Url}", method, targetDescription, targetUrl);

        // Create request
        var httpRequest = new HttpRequestMessage(method, targetUrl);

        // Add S2S token
        httpRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", integration.Token);

        // Add S2S indicator headers
        httpRequest.Headers.TryAddWithoutValidation("X-Linbik-S2S", "true");
        httpRequest.Headers.TryAddWithoutValidation("X-Linbik-Source-Package", _options.SourcePackageName ?? "unknown");
        httpRequest.Headers.TryAddWithoutValidation("X-Linbik-Target-Service-Id", integration.ServiceId.ToString());

        // Add request body for POST/PUT/PATCH
        if (request != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
        {
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        }

        // Send request
        var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        // Read response
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        // Handle error responses
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("S2S request to {Target} failed with {StatusCode}: {Content}",
                targetDescription, response.StatusCode, responseContent);

            // Try to parse as LBaseResponse
            try
            {
                var errorResponse = JsonSerializer.Deserialize<LBaseResponse<TResponse>>(responseContent, JsonOptions);
                if (errorResponse != null)
                    return errorResponse;
            }
            catch
            {
                // Fallback to generic error
            }

            return new LBaseResponse<TResponse>(
                "S2S Error",
                $"Request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        // Deserialize successful response
        try
        {
            var result = JsonSerializer.Deserialize<LBaseResponse<TResponse>>(responseContent, JsonOptions);
            if (result != null)
            {
                logger.LogDebug("S2S request to {Target} completed successfully", targetDescription);
                return result;
            }

            logger.LogWarning("S2S response from {Target} was null after deserialization", targetDescription);
            return new LBaseResponse<TResponse>("S2S Error", "Response deserialization returned null");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize S2S response from {Target}. Content: {Content}",
                targetDescription, responseContent);
            return new LBaseResponse<TResponse>(
                "S2S Error",
                $"Invalid response format from {targetDescription}. Expected LBaseResponse<T>.");
        }
    }

    private LBaseResponse<TResponse> HandleException<TResponse>(Exception ex, string target) where TResponse : class
    {
        return ex switch
        {
            HttpRequestException httpEx =>
                LogAndReturn<TResponse>("HTTP request failed for S2S call to {Target}", target, httpEx,
                    $"Connection failed to {target}: {httpEx.Message}"),

            TaskCanceledException { InnerException: TimeoutException } =>
                LogAndReturn<TResponse>("S2S request to {Target} timed out", target, null,
                    $"Request to {target} timed out"),

            _ => LogAndReturn<TResponse>("Unexpected error during S2S call to {Target}", target, ex,
                    $"Unexpected error: {ex.Message}")
        };
    }

    private LBaseResponse<TResponse> LogAndReturn<TResponse>(
        string logMessage,
        string target,
        Exception? ex,
        string friendlyMessage) where TResponse : class
    {
        if (ex != null)
            logger.LogError(ex, logMessage, target);
        else
            logger.LogWarning(logMessage, target);

        return new LBaseResponse<TResponse>("S2S Error", friendlyMessage);
    }

    private string GetBaseUrlByPackage(string packageName, string? integrationServiceUrl)
    {
        // Prefer ServiceUrl from token response
        if (!string.IsNullOrEmpty(integrationServiceUrl))
            return integrationServiceUrl.TrimEnd('/');

        // Fallback to YARP configuration
        if (_options.IntegrationServices.TryGetValue(packageName, out var serviceConfig))
            return serviceConfig.TargetBaseUrl.TrimEnd('/');

        throw new InvalidOperationException($"No base URL configured for {packageName}");
    }

    private static string BuildTargetUrl(string baseUrl, string endpoint)
    {
        var cleanEndpoint = endpoint.TrimStart('/');
        return $"{baseUrl}/{cleanEndpoint}";
    }

    #endregion
}
