using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Linbik.YARP.Configuration;
using Linbik.YARP.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Linbik.YARP.OpenApi;

public sealed class DelegatedDocumentCache(
    IHttpClientFactory clients, IApplicationTokenProvider tokens, IOptions<YARPOptions> yarp,
    IOptions<LinbikDelegatedOpenApiOptions> settings, IHostEnvironment environment,
    ILogger<DelegatedDocumentCache> logger) : BackgroundService
{
    public const string HttpClientName = "Linbik.YARP.DelegatedOpenApi";
    private readonly ConcurrentDictionary<string, string> documents = new();
    private readonly SemaphoreSlim refreshLock = new(1);
    private volatile bool initialized;

    public async Task<IReadOnlyDictionary<string, string>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        if (!initialized)
            await RefreshAsync(onlyIfUninitialized: true, cancellationToken);
        return new Dictionary<string, string>(documents);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RefreshAsync(onlyIfUninitialized: true, stoppingToken);
            using var timer = new PeriodicTimer(settings.Value.RefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RefreshAsync(onlyIfUninitialized: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task RefreshAsync(bool onlyIfUninitialized, CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (onlyIfUninitialized && initialized) return;
            foreach (var (package, config) in yarp.Value.IntegrationServices)
            {
                if (string.IsNullOrWhiteSpace(config.DelegatedDocumentPath)) continue;
                var url = config.TargetBaseUrl.TrimEnd('/') + "/" + config.DelegatedDocumentPath.TrimStart('/');
                var cachePath = GetCachePath(package, config, url);
                if (!documents.ContainsKey(package) && cachePath is not null && File.Exists(cachePath))
                {
                    try
                    {
                        var cached = await File.ReadAllTextAsync(cachePath, cancellationToken);
                        Validate(cached, package, config.SourcePath, config.TargetPath);
                        documents[package] = cached;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    { logger.LogWarning(ex, "Ignoring invalid cached delegated document for {Package}", package); }
                }
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(yarp.Value.DocumentCheckTimeoutSeconds));
                    var integration = await tokens.GetApplicationIntegrationAsync(package, timeout.Token);
                    if (string.IsNullOrWhiteSpace(integration?.Token))
                        throw new InvalidOperationException("Application token unavailable.");
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", integration.Token);
                    using var client = clients.CreateClient(HttpClientName);
                    using var response = await client.SendAsync(request, timeout.Token);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(timeout.Token);
                    Validate(json, package, config.SourcePath, config.TargetPath);
                    documents[package] = json;
                    if (cachePath is not null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                        var temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            await File.WriteAllTextAsync(temporary, json, cancellationToken);
                            File.Move(temporary, cachePath, overwrite: true);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not refresh delegated OpenAPI for {Package}; last successful document retained (available: {Available})",
                        package, documents.ContainsKey(package));
                }
            }
            initialized = true;
        }
        finally { refreshLock.Release(); }
    }

    private string? GetCachePath(string package, IntegrationServiceOptions config, string url)
    {
        if (settings.Value.CacheDirectory is not { } directory) return null;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(package + "\n" + url + "\n" + config.SourcePath + "\n" + config.TargetPath)));
        return Path.Combine(environment.ContentRootPath, directory, key + ".json");
    }

    private static void Validate(string json, string package, string sourcePath, string targetPath)
    {
        var source = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid OpenAPI JSON.");
        var rewritten = DelegatedDocumentMerger.Merge(new JsonObject
        { ["openapi"] = "3.1.0", ["info"] = new JsonObject { ["title"] = "validation", ["version"] = "1" } }, source, package, sourcePath, targetPath);
        _ = DelegatedDocumentReader.Read(rewritten);
    }
}
