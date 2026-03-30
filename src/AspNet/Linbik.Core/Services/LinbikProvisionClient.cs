using Linbik.Core.Configuration;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Linbik.Core.Services;

/// <summary>
/// Handles automatic provisioning for Keyless Mode.
/// Calls POST /api/dev/provision and manages credential lifecycle.
/// </summary>
public sealed class LinbikProvisionClient(
    IHttpClientFactory httpClientFactory,
    ILinbikCredentialStore credentialStore,
    IOptions<LinbikOptions> options,
    ILogger<LinbikProvisionClient> logger)
{
    private static readonly SemaphoreSlim _provisionLock = new(1, 1);
    private bool _isProvisioned;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Ensure the application has valid credentials.
    /// If cached credentials exist and are valid, use them.
    /// Otherwise, call the provision API and cache the result.
    /// Updates LinbikOptions in-place so downstream services use the provisioned values.
    /// </summary>
    public async Task EnsureProvisionedAsync(CancellationToken cancellationToken = default)
    {
        if (_isProvisioned)
            return;

        await _provisionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isProvisioned)
                return;

            var opts = options.Value;
            if (!opts.KeylessMode)
                return;

            // Already has credentials configured (not keyless)
            if (!string.IsNullOrEmpty(opts.ServiceId) && !string.IsNullOrEmpty(opts.ApiKey) && opts.Clients.Count > 0)
            {
                _isProvisioned = true;
                return;
            }

            // Try loading from cache
            var cached = await credentialStore.LoadAsync(cancellationToken);
            if (cached != null)
            {
                ApplyCredentials(opts, cached);
                _isProvisioned = true;
                logger.LogInformation("Linbik Keyless Mode: Using cached credentials (ServiceId: {ServiceId})", cached.ServiceId);
                return;
            }

            // Provision new service
            var credentials = await ProvisionAsync(opts, cancellationToken);
            if (credentials != null)
            {
                await credentialStore.SaveAsync(credentials, cancellationToken);
                ApplyCredentials(opts, credentials);
                _isProvisioned = true;

                logger.LogInformation(
                    "Linbik Keyless Mode: Service provisioned (ServiceId: {ServiceId}, ClientId: {ClientId}). " +
                    "Claim URL: {ClaimUrl}",
                    credentials.ServiceId, credentials.ClientId,
                    $"{opts.LinbikUrl}/claim/{credentials.ClaimToken}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Linbik Keyless Mode: Provisioning failed. Auth features will be unavailable.");
        }
        finally
        {
            _provisionLock.Release();
        }
    }

    /// <summary>
    /// Update credentials after a successful claim (called from callback handler).
    /// </summary>
    public async Task HandleClaimAsync(string newApiKey, CancellationToken cancellationToken = default)
    {
        var cached = await credentialStore.LoadAsync(cancellationToken);
        if (cached == null)
            return;

        cached.ApiKey = newApiKey;
        cached.IsClaimed = true;
        cached.ClaimToken = null;

        await credentialStore.SaveAsync(cached, cancellationToken);

        // Update options in-place
        options.Value.ApiKey = newApiKey;

        logger.LogInformation("Linbik Keyless Mode: Service successfully claimed! New permanent API key applied.");
    }

    private async Task<LinbikCredentials?> ProvisionAsync(LinbikOptions opts, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("LinbikAuthClient");

        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
        var sdkVersion = typeof(LinbikProvisionClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        // Detect app URL from Kestrel configuration
        var appUrl = opts.Clients.FirstOrDefault()?.BaseUrl ?? "https://localhost:5001";

        var request = new ProvisionRequestDto
        {
            AppName = appName,
            AppUrl = appUrl,
            CallbackPath = opts.Clients.FirstOrDefault()?.RedirectUrl ?? "/auth/callback",
            Platform = "aspnet"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/dev/provision");
        httpRequest.Headers.Add(LinbikDefaults.HeaderMode, "Keyless");
        httpRequest.Headers.Add(LinbikDefaults.HeaderVersion, sdkVersion);
        httpRequest.Headers.Add(LinbikDefaults.HeaderPlatform, "aspnet");
        httpRequest.Content = JsonContent.Create(request, options: _jsonOptions);

        var response = await client.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Provision failed with status {Status}: {Body}", response.StatusCode, errorBody);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ProvisionApiResponse>(_jsonOptions, cancellationToken);
        if (result?.Data == null)
        {
            logger.LogWarning("Provision response had no data.");
            return null;
        }

        return new LinbikCredentials
        {
            ServiceId = result.Data.ServiceId.ToString(),
            ClientId = result.Data.ClientId.ToString(),
            ApiKey = result.Data.ApiKey,
            ClaimToken = result.Data.ClaimToken,
            IsClaimed = false,
            ProvisionedAt = DateTime.UtcNow,
            ExpiresAt = result.Data.ExpiresAt
        };
    }

    private static void ApplyCredentials(LinbikOptions opts, LinbikCredentials credentials)
    {
        opts.ServiceId = credentials.ServiceId;
        opts.ApiKey = credentials.ApiKey;

        if (opts.Clients.Count == 0)
        {
            opts.Clients.Add(new LinbikClientConfig
            {
                ClientId = credentials.ClientId,
                ClientType = LinbikClientType.Web
            });
        }
        else
        {
            opts.Clients[0].ClientId = credentials.ClientId;
        }
    }

    #region Internal DTOs

    private sealed class ProvisionRequestDto
    {
        public string AppName { get; set; } = string.Empty;
        public string AppUrl { get; set; } = string.Empty;
        public string CallbackPath { get; set; } = "/auth/callback";
        public string Platform { get; set; } = "aspnet";
    }

    private sealed class ProvisionApiResponse
    {
        public ProvisionDataDto? Data { get; set; }
    }

    private sealed class ProvisionDataDto
    {
        public Guid ServiceId { get; set; }
        public Guid ClientId { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string ClaimToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    #endregion
}
