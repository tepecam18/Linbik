using Linbik.Core.Configuration;
using Linbik.Core.Responses;
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
    public async Task EnsureProvisionedAsync(string appUrl, string callbackPath, string? clientName, CancellationToken cancellationToken = default)
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
                await EnsureValidationKeyAsync(opts, null, cancellationToken);
                _isProvisioned = true;
                return;
            }

            // Try loading from cache
            var cached = await credentialStore.LoadAsync(cancellationToken);
            if (cached != null)
            {
                await EnsureValidationKeyAsync(opts, cached, cancellationToken);
                ApplyCredentials(opts, cached);
                _isProvisioned = true;
                logger.LogInformation("Linbik Keyless Mode: Using cached credentials (ServiceId: {ServiceId})", cached.ServiceId);
                return;
            }

            // Provision new service
            var provisionResult = await ProvisionAsync(appUrl, callbackPath, opts, cancellationToken);
            if (!provisionResult.IsSuccess || provisionResult.Data == null)
            {
                var errorMsg = provisionResult.FriendlyMessage?.Message ?? "Unknown provisioning error.";
                logger.LogWarning("Linbik Keyless Mode: Provisioning failed — {Error}", errorMsg);
                return;
            }

            var credentials = provisionResult.Data;
            credentials.ClientName = clientName ?? "Default";
            await credentialStore.SaveAsync(credentials, cancellationToken);
            ApplyCredentials(opts, credentials);
            _isProvisioned = true;

            logger.LogInformation(
                "Linbik Keyless Mode: Service provisioned (ServiceId: {ServiceId}, ClientId: {ClientId}). " +
                "Claim URL: {ClaimUrl}",
                credentials.ServiceId, credentials.ClientId,
                $"{credentials.ClaimUrl}");
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

    private async Task<LBaseResponse<LinbikCredentials>> ProvisionAsync(string appUrl, string callbackPath, LinbikOptions opts, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("LinbikAuthClient");

        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
        var sdkVersion = typeof(LinbikProvisionClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        var request = new ProvisionRequestDto
        {
            AppName = appName,
            AppUrl = appUrl ?? "http://localhost",
            CallbackPath = callbackPath ?? "/api/linbik/login",
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
            return new LBaseResponse<LinbikCredentials>("provision_failed", $"HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<ProvisionApiResponse>(_jsonOptions, cancellationToken);
        if (result?.Data == null)
        {
            logger.LogWarning("Provision response had no data.");
            return new LBaseResponse<LinbikCredentials>("provision_empty_response", "Server returned empty provisioning data.");
        }

        return new LBaseResponse<LinbikCredentials>(new LinbikCredentials
        {
            ServiceId = result.Data.ServiceId.ToString(),
            ClientId = result.Data.ClientId.ToString(),
            ApiKey = result.Data.ApiKey,
            PublicKey = result.Data.PublicKey,
            PackageName = result.Data.PackageName,
            ClaimToken = result.Data.ClaimToken,
            ClaimUrl = result.Data.ClaimUrl,
            IsClaimed = false,
            ProvisionedAt = DateTime.UtcNow
        });
    }

    private static void ApplyCredentials(LinbikOptions opts, LinbikCredentials credentials)
    {
        opts.ServiceId = credentials.ServiceId;
        opts.ApiKey = credentials.ApiKey;
        opts.PublicKey = credentials.PublicKey;
        opts.PackageName = credentials.PackageName;

        var existingClient = opts.Clients.FirstOrDefault(c => c.Name == credentials.ClientName);

        if (existingClient != null)
        {
            existingClient.ClientId = credentials.ClientId;
            return;
        }

        opts.Clients.Add(new LinbikClientConfig
        {
            ClientId = credentials.ClientId,
            Name = credentials.ClientName,
            RedirectUrl = "/"
        });
    }

    private async Task EnsureValidationKeyAsync(LinbikOptions opts, LinbikCredentials? cached, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(cached?.PublicKey ?? opts.PublicKey) &&
            !string.IsNullOrEmpty(cached?.PackageName ?? opts.PackageName))
            return;

        var serviceId = cached?.ServiceId ?? opts.ServiceId;
        if (!Guid.TryParse(serviceId, out var id) || id == Guid.Empty)
            throw new InvalidOperationException("Linbik:ServiceId (or the cached provisioning ServiceId) must be a non-empty UUID from your Linbik service registration before validation keys can be recovered. ServiceId is not a ClientId or IntegrationApp.Id.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/dev/{id}/validation-key");
        request.Headers.Add("apiKey", cached?.ApiKey ?? opts.ApiKey);
        using var response = await httpClientFactory.CreateClient("LinbikAuthClient").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProvisionApiResponse>(_jsonOptions, cancellationToken);
        var data = result?.Data;
        if (string.IsNullOrWhiteSpace(data?.PublicKey) || string.IsNullOrWhiteSpace(data.PackageName))
            throw new InvalidOperationException("The validation-key response did not contain both PublicKey and PackageName. Verify the ServiceId and API key for this service, or configure Linbik:PublicKey and Linbik:PackageName from its registration. Do not include API keys in error reports.");
        if (cached != null)
        {
            cached.PublicKey = data.PublicKey;
            cached.PackageName = data.PackageName;
            await credentialStore.SaveAsync(cached, cancellationToken);
        }
        opts.PublicKey = data.PublicKey;
        opts.PackageName = data.PackageName;
    }

    #region Internal DTOs

    private sealed class ProvisionRequestDto
    {
        public string AppName { get; set; } = string.Empty;
        public string AppUrl { get; set; } = string.Empty;
        public string CallbackPath { get; set; } = "/api/linbik/callback";
        public string Platform { get; set; } = "aspnet";
    }

    private sealed class ProvisionApiResponse
    {
        public ProvisionDataDto? Data { get; set; }
    }

    private sealed class ProvisionDataDto
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public Guid ServiceId { get; set; }
        public Guid ClientId { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string ClaimToken { get; set; } = string.Empty;
        public string ClaimUrl { get; set; } = string.Empty;
    }

    #endregion
}
