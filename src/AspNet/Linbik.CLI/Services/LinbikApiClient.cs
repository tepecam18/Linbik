using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Linbik.CLI.Services;

/// <summary>
/// HTTP client for Linbik API interactions.
/// Handles provisioning, token exchange, and service management.
/// </summary>
internal sealed class LinbikApiClient : IDisposable
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public LinbikApiClient(string linbikUrl)
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri(linbikUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.Add("X-Linbik-Mode", "CLI");
        _client.DefaultRequestHeaders.Add("X-Linbik-Platform", "dotnet-cli");
    }

    /// <summary>
    /// Provision a new service via POST /api/dev/provision
    /// </summary>
    public async Task<ProvisionResponse?> ProvisionAsync(string appName, string appUrl, string callbackPath, CancellationToken ct = default)
    {
        var request = new
        {
            appName,
            appUrl,
            callbackPath,
            platform = "aspnet"
        };

        var response = await _client.PostAsJsonAsync("/api/dev/provision", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            ConsoleUI.Error($"Provision failed ({response.StatusCode}): {error}");
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProvisionResponse>>(JsonOptions, ct);
        return result?.Data;
    }

    /// <summary>
    /// Exchange authorization code for tokens via POST /oauth/token
    /// </summary>
    public async Task<TokenResponse?> ExchangeCodeAsync(string code, string serviceId, string apiKey, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token");
        request.Headers.Add("Code", code);
        request.Headers.Add("ApiKey", apiKey);
        request.Content = JsonContent.Create(new { serviceId }, options: JsonOptions);

        var response = await _client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            ConsoleUI.Error($"Token exchange failed ({response.StatusCode}): {error}");
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenResponse>>(JsonOptions, ct);
        return result?.Data;
    }

    /// <summary>
    /// Get service status via GET /api/services/{serviceId}
    /// </summary>
    public async Task<ServiceStatusResponse?> GetServiceStatusAsync(string serviceId, string apiKey, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/services/{serviceId}");
        request.Headers.Add("ApiKey", apiKey);

        var response = await _client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            ConsoleUI.Error($"Status check failed ({response.StatusCode}): {error}");
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ServiceStatusResponse>>(JsonOptions, ct);
        return result?.Data;
    }

    /// <summary>
    /// Start a temporary HTTP listener for OAuth callback.
    /// Returns the port it's listening on.
    /// </summary>
    public static (HttpListener listener, int port) StartCallbackListener()
    {
        // Find a free port
        var tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        return (listener, port);
    }

    /// <summary>
    /// Wait for the OAuth callback with authorization code.
    /// Returns the code from query string.
    /// </summary>
    public static async Task<string?> WaitForCallbackAsync(HttpListener listener, CancellationToken ct = default)
    {
        try
        {
            var context = await listener.GetContextAsync().WaitAsync(ct);
            var code = context.Request.QueryString["code"];

            // Send response to the browser
            var responseHtml = """
                <html><body style="font-family:system-ui;text-align:center;padding:60px;">
                <h2 style="color:#7c3aed;">✅ Linbik CLI</h2>
                <p>Giriş başarılı! Bu pencereyi kapatabilirsiniz.</p>
                <script>setTimeout(()=>window.close(),2000)</script>
                </body></html>
                """;

            var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, ct);
            context.Response.Close();

            return code;
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    public void Dispose() => _client.Dispose();
}

#region Response Models

internal sealed class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
}

internal sealed class ProvisionResponse
{
    public Guid ServiceId { get; set; }
    public Guid ClientId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ClaimToken { get; set; } = string.Empty;
    public string ClaimUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

internal sealed class TokenResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool? Claimed { get; set; }
    public string? NewApiKey { get; set; }
}

internal sealed class ServiceStatusResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public bool IsProvisioned { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ClientInfo>? Clients { get; set; }
}

internal sealed class ClientInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

#endregion
