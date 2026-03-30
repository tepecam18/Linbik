using Linbik.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Linbik.Core.Services;

/// <summary>
/// Background service that sends periodic heartbeat signals to the Linbik server.
/// Reports SDK health, uptime, and version for dashboard monitoring.
/// Uses exponential backoff on failure (60s → 120s → 240s, max 5 minutes).
/// </summary>
public sealed class LinbikHeartbeatService(
    IHttpClientFactory httpClientFactory,
    IOptions<LinbikOptions> options,
    ILogger<LinbikHeartbeatService> logger) : BackgroundService
{
    private readonly LinbikOptions _options = options.Value;
    private readonly DateTime _startTime = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableHeartbeat)
        {
            logger.LogInformation("Linbik heartbeat is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ServiceId) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("Linbik heartbeat skipped: ServiceId or ApiKey is not configured.");
            return;
        }

        // Initial delay before first heartbeat
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var baseInterval = TimeSpan.FromSeconds(
            Math.Clamp(_options.HeartbeatIntervalSeconds, 10, 600));
        var currentInterval = baseInterval;
        const int maxBackoffSeconds = 300; // 5 minutes

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var success = await SendHeartbeatAsync(stoppingToken);

                // Reset interval on success, increase on failure
                currentInterval = success
                    ? baseInterval
                    : TimeSpan.FromSeconds(Math.Min(currentInterval.TotalSeconds * 2, maxBackoffSeconds));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in heartbeat loop.");
                currentInterval = TimeSpan.FromSeconds(
                    Math.Min(currentInterval.TotalSeconds * 2, maxBackoffSeconds));
            }

            try
            {
                await Task.Delay(currentInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        // Pick the first client ID if available
        var clientId = _options.Clients.FirstOrDefault()?.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogDebug("No client configured, skipping heartbeat.");
            return false;
        }

        var uptime = (long)(DateTime.UtcNow - _startTime).TotalSeconds;
        var version = typeof(LinbikHeartbeatService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        var payload = new
        {
            serviceId = Guid.Parse(_options.ServiceId),
            clientId = Guid.Parse(clientId),
            sdkVersion = version,
            uptime,
            platform = "aspnet",
            mode = "standard"
        };

        using var httpClient = httpClientFactory.CreateClient(Extensions.LinbikServiceCollectionExtensions.LinbikHttpClientName);
        var request = new HttpRequestMessage(HttpMethod.Post, "api/heartbeat")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        request.Headers.TryAddWithoutValidation("ApiKey", _options.ApiKey);
        request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderMode, "sdk");
        request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderPlatform, "aspnet");
        request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderVersion, version);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Heartbeat failed with status {StatusCode}.", response.StatusCode);
            return false;
        }

        logger.LogDebug("Heartbeat sent successfully (uptime: {Uptime}s).", uptime);
        return true;
    }
}
