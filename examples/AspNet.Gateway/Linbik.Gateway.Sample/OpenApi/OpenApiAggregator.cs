using System.Text.Json;
using System.Text.Json.Nodes;
using Yarp.ReverseProxy.Configuration;

namespace Linbik.Gateway.Sample.OpenApi;

/// <summary>
/// Downstream servislerinin OpenAPI dokümanlarını toplayan ve
/// <c>x-linbik-audiences</c> extension'ına göre <b>üç ayrı dokümana</b>
/// (self / delegated / apps) ayıran servis.
///
/// <para>
/// Üyelik kararı öncelik sırasıyla:
/// <list type="number">
///   <item>Downstream operation'daki <c>x-linbik-audiences</c> extension</item>
///   <item>YARP cluster metadata'sındaki <c>linbik:audiences</c> (virgüllü liste)</item>
///   <item>Route URL prefix'inden türetim (/self/**, /delegated/**, /apps/**)</item>
/// </list>
/// Bir operation birden fazla audience listesindeyse her birinin dokümanına eklenir
/// (N:N). Path, ilgili audience prefix'i ile yeniden yazılır.
/// </para>
/// </summary>
public sealed class OpenApiAggregator
{
    private readonly IProxyConfigProvider _proxyConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiAggregator> _logger;

    // In-memory cache: audience → doküman JSON, zaman damgası
    private readonly Dictionary<string, (JsonObject Doc, DateTimeOffset FetchedAt)> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public OpenApiAggregator(
        IProxyConfigProvider proxyConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenApiAggregator> logger)
    {
        _proxyConfig    = proxyConfig;
        _httpClientFactory = httpClientFactory;
        _logger         = logger;
    }

    /// <summary>
    /// Belirtilen audience için birleştirilmiş OpenAPI dokümanını döner.
    /// Cache süresi dolmuşsa downstream'lerden yeniden çeker.
    /// </summary>
    public async Task<JsonObject?> GetDocumentAsync(string audience, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(audience, out var entry)
                && DateTimeOffset.UtcNow - entry.FetchedAt < CacheTtl)
            {
                return entry.Doc;
            }

            var doc = await BuildDocumentAsync(audience, ct);
            if (doc is not null)
                _cache[audience] = (doc, DateTimeOffset.UtcNow);
            return doc;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── İç yapı ──────────────────────────────────────────────────────────

    private async Task<JsonObject?> BuildDocumentAsync(string audience, CancellationToken ct)
    {
        var config    = _proxyConfig.GetConfig();
        var baseDoc   = CreateBaseDocument(audience);
        var paths     = new JsonObject();

        foreach (var cluster in config.Clusters)
        {
            var destination = cluster.Destinations?.Values.FirstOrDefault();
            if (destination is null) continue;

            string? openApiPath = null;
            cluster.Metadata?.TryGetValue(
                Gateway.LinbikGatewayDefaults.MetadataOpenApiPath,
                out openApiPath);
            openApiPath ??= "/openapi/v1.json";

            var baseAddress = destination.Address.TrimEnd('/');
            var url = $"{baseAddress}{openApiPath}";

            JsonObject? downstreamDoc;
            try
            {
                using var client = _httpClientFactory.CreateClient("OpenApiAggregator");
                var json = await client.GetStringAsync(url, ct);
                downstreamDoc = JsonNode.Parse(json)?.AsObject();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cluster {ClusterId} OpenAPI dokümanı çekilemedi: {Url}",
                    cluster.ClusterId, url);
                continue;
            }

            if (downstreamDoc is null) continue;

            // Cluster'a ait route'lardan audience → path prefix eşlemesi
            var routePrefixMap = BuildRoutePrefixMap(config.Routes, cluster.ClusterId, audience);

            MergePaths(paths, downstreamDoc, routePrefixMap, audience, cluster);
        }

        baseDoc["paths"] = paths;
        return baseDoc;
    }

    /// <summary>
    /// Verilen cluster'a yönlendiren route'ları tarar; bu audience'a ait
    /// olanların path prefix'ini döner.
    /// </summary>
    private static Dictionary<string, string> BuildRoutePrefixMap(
        IReadOnlyList<RouteConfig> routes,
        string clusterId,
        string targetAudience)
    {
        // originalPath → prefixToAdd eşlemesi (örn. "/echo" → "/self")
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in routes.Where(r => r.ClusterId == clusterId))
        {
            var routeAudiences = GetRouteAudiences(route);
            if (!routeAudiences.Contains(targetAudience, StringComparer.OrdinalIgnoreCase))
                continue;

            // Route match path: "/self/{**catchall}" → prefix = "/self"
            var matchPath = route.Match.Path ?? string.Empty;
            var prefix = matchPath.Split("/{").FirstOrDefault()?.TrimEnd('/') ?? string.Empty;

            // RemovePrefix transform'undan downstream prefix'ini bul
            // (appsettings'te "PathRemovePrefix": "/self")
            var removePrefix = route.Transforms?
                .SelectMany(t => t)
                .Where(kv => kv.Key == "PathRemovePrefix")
                .Select(kv => kv.Value)
                .FirstOrDefault() ?? prefix;

            map[removePrefix] = prefix;
        }

        return map;
    }

    /// <summary>
    /// Downstream OpenAPI dokümanındaki path'leri, audience filtresi uygulanarak
    /// hedef dokümana birleştirir. Path'ler audience prefix'i ile yeniden yazılır.
    /// </summary>
    private static void MergePaths(
        JsonObject targetPaths,
        JsonObject sourceDoc,
        Dictionary<string, string> routePrefixMap,
        string targetAudience,
        ClusterConfig cluster)
    {
        if (sourceDoc["paths"] is not JsonObject sourcePaths) return;

        foreach (var (path, pathItem) in sourcePaths)
        {
            if (pathItem is not JsonObject pathObj) continue;

            foreach (var (method, operationNode) in pathObj)
            {
                if (operationNode is not JsonObject operation) continue;

                // Operation'a özgü audience listesini belirle
                var audiences = ResolveOperationAudiences(operation, cluster, path);

                if (!audiences.Contains(targetAudience, StringComparer.OrdinalIgnoreCase))
                    continue;

                // Path'i doğru prefix ile yeniden yaz
                var rewrittenPath = RewritePath(path, routePrefixMap, targetAudience);

                // Hedef dokümana ekle
                if (targetPaths[rewrittenPath] is not JsonObject existingItem)
                {
                    existingItem = new JsonObject();
                    targetPaths[rewrittenPath] = existingItem;
                }

                // x-linbik-audiences extension'ını temizle (downstream'e özgü, gateway dokümanında gereksiz)
                var cleanOp = JsonNode.Parse(operation.ToJsonString())!.AsObject();
                cleanOp.Remove("x-linbik-audiences");
                existingItem[method] = cleanOp;
            }
        }
    }

    private static string[] ResolveOperationAudiences(
        JsonObject operation,
        ClusterConfig cluster,
        string path)
    {
        // 1. Operation extension
        if (operation["x-linbik-audiences"] is JsonArray extArr)
        {
            return extArr.Select(n => n?.GetValue<string>() ?? string.Empty)
                         .Where(s => !string.IsNullOrEmpty(s))
                         .ToArray();
        }

        // 2. Cluster metadata
        if (cluster.Metadata?.TryGetValue(
                Gateway.LinbikGatewayDefaults.MetadataAudiences, out var metaAudiences) == true
            && !string.IsNullOrEmpty(metaAudiences))
        {
            return metaAudiences.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // 3. Path prefix'inden türet
        return path.TrimStart('/').Split('/').FirstOrDefault() switch
        {
            "self"      => ["self"],
            "delegated" => ["delegated"],
            "apps"      => ["apps"],
            _ => ["self"]  // fallback
        };
    }

    private static IEnumerable<string> GetRouteAudiences(RouteConfig route)
    {
        if (route.Metadata?.TryGetValue(
                Gateway.LinbikGatewayDefaults.MetadataAudiences, out var audiences) == true
            && !string.IsNullOrEmpty(audiences))
        {
            return audiences.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return [];
    }

    private static string RewritePath(
        string originalPath,
        Dictionary<string, string> routePrefixMap,
        string audience)
    {
        // Route prefix map'ten uygun prefix'i bul
        foreach (var (removePrefix, addPrefix) in routePrefixMap)
        {
            if (originalPath.StartsWith(removePrefix, StringComparison.OrdinalIgnoreCase))
                return addPrefix + originalPath[removePrefix.Length..];
        }

        // Fallback: audience prefix ekle
        return $"/{audience}{originalPath}";
    }

    private static JsonObject CreateBaseDocument(string audience)
    {
        return new JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new JsonObject
            {
                ["title"]   = $"Linbik Gateway — {char.ToUpperInvariant(audience[0])}{audience[1..]}",
                ["version"] = "v1",
                ["description"] = $"Gateway OpenAPI dokümanı ({audience}). " +
                                  "Bu doküman downstream servislerden toplanmıştır."
            },
            ["paths"] = new JsonObject()
        };
    }
}
