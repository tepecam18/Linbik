using Linbik.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Linbik.Core.HealthChecks;

/// <summary>
/// Health check for Linbik authentication server connectivity.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the Linbik authentication server is reachable
/// and responding to requests. It performs a simple connectivity test without
/// actually authenticating.
/// </para>
/// <para>
/// The health check is automatically registered when calling 
/// <c>AddLinbikHealthChecks()</c> on the service collection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In Program.cs
/// builder.Services.AddLinbik(builder.Configuration);
/// builder.Services.AddLinbikHealthChecks();
/// 
/// // ...
/// 
/// app.MapHealthChecks("/health");
/// </code>
/// </example>
public class LinbikServerHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly LinbikOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikServerHealthCheck"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="options">The Linbik configuration options.</param>
    public LinbikServerHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<LinbikOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("LinbikHealthCheck");
        _options = options.Value;
    }

    /// <summary>
    /// Runs the health check, returning the status of the Linbik server.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="HealthCheckResult"/> representing the outcome of the health check.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = _options.LinbikUrl.TrimEnd('/');

            // Try to reach the Linbik server with a simple HEAD request
            var request = new HttpRequestMessage(HttpMethod.Head, baseUrl);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    $"Linbik server at {baseUrl} is reachable.",
                    new Dictionary<string, object>
                    {
                        ["url"] = baseUrl,
                        ["statusCode"] = (int)response.StatusCode
                    });
            }

            // Server responded but with error status
            return HealthCheckResult.Degraded(
                $"Linbik server responded with status {response.StatusCode}.",
                data: new Dictionary<string, object>
                {
                    ["url"] = baseUrl,
                    ["statusCode"] = (int)response.StatusCode,
                    ["reasonPhrase"] = response.ReasonPhrase ?? "Unknown"
                });
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to connect to Linbik server.",
                ex,
                new Dictionary<string, object>
                {
                    ["url"] = _options.LinbikUrl,
                    ["error"] = ex.Message
                });
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            return HealthCheckResult.Unhealthy(
                "Health check was cancelled.",
                ex);
        }
        catch (TaskCanceledException ex)
        {
            return HealthCheckResult.Unhealthy(
                "Health check timed out.",
                ex,
                new Dictionary<string, object>
                {
                    ["url"] = _options.LinbikUrl
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "An unexpected error occurred during health check.",
                ex,
                new Dictionary<string, object>
                {
                    ["url"] = _options.LinbikUrl,
                    ["errorType"] = ex.GetType().Name
                });
        }
    }
}
