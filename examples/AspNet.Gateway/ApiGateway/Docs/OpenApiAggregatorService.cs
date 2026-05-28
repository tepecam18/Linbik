using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace ApiGateway.Docs;

/// <summary>
/// Periyodik (varsayılan 60s) olarak konfigüre edilen downstream servislerden OpenAPI
/// dokümanlarını çeker. ETag ile koşullu GET kullanır; 304'te cache korunur. ETag yaşı
/// <see cref="LinbikGatewayOptions.EtagMaxAgeSeconds"/>'i aştığında <c>If-None-Match</c>
/// göndermeden full fetch yapar — kalıcı stale veya cache poisoning koruması.
/// Hata olursa fail-open: önceki snapshot saklanır.
/// </summary>
public sealed class OpenApiAggregatorService : BackgroundService
{
    private readonly LinbikGatewayOptions _options;
    private readonly OpenApiCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiAggregatorService> _logger;
    private readonly SemaphoreSlim _initialFetchGate = new(1, 1);
    private volatile bool _initialFetchAttempted;

    public OpenApiAggregatorService(
        IOptions<LinbikGatewayOptions> options,
        OpenApiCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenApiAggregatorService> logger)
    {
        _options = options.Value;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Lazy first-load: ilk dış istek geldiğinde, eğer henüz fetch yapılmadıysa
    /// tek seferlik tetikleme. Concurrent çağrıları semaforla serileştirir.
    /// </summary>
    public async Task EnsureInitialFetchAsync(CancellationToken ct)
    {
        if (_initialFetchAttempted || _cache.HasAny) return;

        await _initialFetchGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialFetchAttempted || _cache.HasAny) return;
            _initialFetchAttempted = true;
            await RefreshAllAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _initialFetchGate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _options.RefreshIntervalSeconds > 0
            ? TimeSpan.FromSeconds(_options.RefreshIntervalSeconds)
            : TimeSpan.Zero;

        try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllAsync(stoppingToken).ConfigureAwait(false);
                _initialFetchAttempted = true;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAPI aggregator refresh cycle hata aldı; sonraki cycle'da tekrar denenecek.");
            }

            if (interval <= TimeSpan.Zero) return;

            try { await Task.Delay(interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RefreshAllAsync(CancellationToken ct)
    {
        if (_options.Sources.Count == 0)
        {
            _logger.LogDebug("LinbikGateway:Sources boş; aggregator atlandı.");
            return;
        }

        var tasks = _options.Sources.Select(s => RefreshOneAsync(s, ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task RefreshOneAsync(LinbikGatewaySource source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source.ServicePrefix) || string.IsNullOrWhiteSpace(source.Address))
            return;

        var address = source.Address.TrimEnd('/');
        var url = address + _options.DownstreamOpenApiPath;
        var existing = _cache.TryGet(source.ServicePrefix);

        var etagExpired = existing is not null &&
            (DateTimeOffset.UtcNow - existing.EtagSetAt) > TimeSpan.FromSeconds(Math.Max(1, _options.EtagMaxAgeSeconds));
        var useConditional = existing is not null && !etagExpired && !string.IsNullOrWhiteSpace(existing.ETag);

        var client = _httpClientFactory.CreateClient(nameof(OpenApiAggregatorService));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.FetchTimeoutSeconds));

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (useConditional)
            {
                req.Headers.TryAddWithoutValidation("If-None-Match", existing!.ETag);
            }

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotModified && existing is not null)
            {
                _logger.LogDebug("'{Service}' OpenAPI 304 Not Modified.", source.ServicePrefix);
                return;
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("'{Service}' OpenAPI fetch {Status} ({Url}); önceki snapshot korunuyor.",
                    source.ServicePrefix, (int)resp.StatusCode, url);
                return;
            }

            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            JsonNode? node;
            try { node = JsonNode.Parse(raw); }
            catch (Exception parseEx)
            {
                _logger.LogWarning(parseEx, "'{Service}' OpenAPI parse hatası.", source.ServicePrefix);
                return;
            }
            if (node is null) return;

            var snapshot = new DownstreamOpenApiSnapshot(
                ServicePrefix: source.ServicePrefix,
                DisplayName: source.DisplayName,
                ETag: resp.Headers.ETag?.Tag,
                EtagSetAt: DateTimeOffset.UtcNow,
                RawJson: raw,
                Document: node,
                FetchedAt: DateTimeOffset.UtcNow,
                IsGateway: source.IsGateway);

            _cache.Upsert(snapshot);
            _logger.LogInformation("'{Service}' OpenAPI güncellendi (ETag={ETag}, EtagExpired={Expired}).",
                source.ServicePrefix, snapshot.ETag ?? "(yok)", etagExpired);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            if (existing is null)
                _logger.LogWarning(ex, "'{Service}' ilk fetch başarısız ({Url}).", source.ServicePrefix, url);
            else
                _logger.LogDebug(ex, "'{Service}' refresh başarısız; eski snapshot ({FetchedAt}) korunuyor.",
                    source.ServicePrefix, existing.FetchedAt);
        }
    }
}
