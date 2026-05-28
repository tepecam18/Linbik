using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiGateway.Docs;

/// <summary>
/// Aggregator cache'indeki downstream OpenAPI dokümanlarını flow'a göre filtreleyip
/// path'leri gateway-public hâle çevirerek tek bir birleşik OpenAPI dokümanı üretir.
/// Üç akış: <see cref="FlowSelf"/>, <see cref="FlowDelegated"/>, <see cref="FlowApplication"/>.
/// </summary>
public static class FilteredDocumentBuilder
{
    public const string FlowSelf = "Self";
    public const string FlowDelegated = "Delegated";
    public const string FlowApplication = "Application";
    public const string AnonymousMarker = "*";
    public const string AuthenticatedMarker = "authenticated";
    public const string FlowExtensionKey = "linbik-flows";

    /// <summary>
    /// Verilen akış için filtrelenmiş OpenAPI JSON dokümanını üretir. Hiç eşleşen
    /// operation yoksa bile geçerli (boş paths) bir doküman döner.
    /// </summary>
    public static string Build(IReadOnlyList<DownstreamOpenApiSnapshot> sources, string flow)
    {
        var publicPrefix = ResolvePublicPrefix(flow);
        var docTitle = flow switch
        {
            FlowSelf => "Linbik Gateway — Self (cookie session)",
            FlowDelegated => "Linbik Gateway — Delegated (user-delegated bearer)",
            FlowApplication => "Linbik Gateway — Application (S2S bearer)",
            _ => "Linbik Gateway"
        };

        var result = new JsonObject
        {
            ["openapi"] = "3.1.0",
            ["info"] = new JsonObject
            {
                ["title"] = docTitle,
                ["version"] = "v1",
                ["description"] = "Gateway aggregator tarafından üretilmiştir. " +
                                   "Yalnız \"" + flow + "\" akışına izin verilen operasyonlar gösterilmektedir."
            },
            ["paths"] = new JsonObject(),
        };

        var paths = (JsonObject)result["paths"]!;
        var tags = new JsonArray();
        var componentsSchemas = new JsonObject();

        foreach (var snap in sources)
        {
            if (snap.Document is not JsonObject downstream) continue;

            // Tag (servis grubu)
            var tagName = snap.DisplayName ?? snap.ServicePrefix;
            tags.Add(new JsonObject { ["name"] = tagName });

            // components/schemas'ı kopyala (basit merge — name çakışması üzerine yaz; ileride prefiks eklenebilir)
            if (downstream["components"] is JsonObject comps &&
                comps["schemas"] is JsonObject schemas)
            {
                foreach (var (schemaName, schemaNode) in schemas)
                {
                    if (schemaNode is null) continue;
                    if (!componentsSchemas.ContainsKey(schemaName))
                    {
                        componentsSchemas[schemaName] = schemaNode.DeepClone();
                    }
                }
            }

            if (downstream["paths"] is not JsonObject downstreamPaths) continue;

            foreach (var (path, pathItemNode) in downstreamPaths)
            {
                if (pathItemNode is not JsonObject pathItem) continue;

                string publicPath;
                if (snap.IsGateway)
                {
                    // Gateway kendi endpoint'leri (örn. /api/linbik/login) — path olduğu gibi geçer.
                    // Yalnız Self akışında görünür: cookie tabanlı login/refresh akışları
                    // delegated/apps doc'larında anlamsız.
                    if (!string.Equals(flow, FlowSelf, StringComparison.OrdinalIgnoreCase))
                        continue;
                    publicPath = path;
                }
                else if (!TryRewritePath(path, snap.ServicePrefix, publicPrefix, out publicPath!))
                {
                    // Convention dışı downstream path'leri (örn /health) doc'a alınmaz.
                    continue;
                }

                var rewrittenPathItem = new JsonObject();
                foreach (var (verb, opNode) in pathItem)
                {
                    if (!IsHttpVerb(verb))
                    {
                        // parameters / summary gibi alt alanlar
                        rewrittenPathItem[verb] = opNode?.DeepClone();
                        continue;
                    }

                    if (opNode is not JsonObject op) continue;
                    if (!OperationAllowedForFlow(op, flow)) continue;

                    var clonedOp = (JsonObject)op.DeepClone();

                    // Operation tag'lerini servis adıyla zenginleştir.
                    var opTags = clonedOp["tags"] as JsonArray ?? new JsonArray();
                    if (!opTags.Any(t => string.Equals(t?.GetValue<string>(), tagName, StringComparison.Ordinal)))
                    {
                        opTags.Add(tagName);
                    }
                    clonedOp["tags"] = opTags;

                    rewrittenPathItem[verb] = clonedOp;
                }

                // Hiç verb kalmadıysa path'i ekleme.
                if (rewrittenPathItem.Count(kv => IsHttpVerb(kv.Key)) == 0)
                    continue;

                // Aynı public path birden çok source'tan gelebilir (teorik olarak): merge et.
                if (paths[publicPath] is JsonObject existing)
                {
                    foreach (var (k, v) in rewrittenPathItem)
                    {
                        existing[k] = v?.DeepClone();
                    }
                }
                else
                {
                    paths[publicPath] = rewrittenPathItem;
                }
            }
        }

        result["tags"] = tags;
        if (componentsSchemas.Count > 0)
        {
            result["components"] = new JsonObject { ["schemas"] = componentsSchemas };
        }

        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string ResolvePublicPrefix(string flow) => flow switch
    {
        FlowSelf => string.Empty,            // /{servicePrefix}/...
        FlowDelegated => "/delegated",
        FlowApplication => "/apps",
        _ => string.Empty
    };

    private static bool TryRewritePath(string downstreamPath, string servicePrefix, string publicPrefix, out string publicPath)
    {
        // Downstream beklenen format: /api/{servicePrefix}/{rest}
        var expectedPrefix = "/api/" + servicePrefix;
        if (downstreamPath.StartsWith(expectedPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = downstreamPath.Substring(expectedPrefix.Length); // "/rest"
            publicPath = publicPrefix + "/" + servicePrefix + rest;
            return true;
        }
        if (string.Equals(downstreamPath, expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            publicPath = publicPrefix + "/" + servicePrefix;
            return true;
        }
        // Convention dışı path'ler (örn /health) doc'a alınmaz.
        publicPath = string.Empty;
        return false;
    }

    private static bool OperationAllowedForFlow(JsonObject op, string flow)
    {
        if (op[FlowExtensionKey] is not JsonArray flows)
        {
            // linbik-flows yoksa: kaynak transformer çalıştırılmamış demek. Conservative: gizle.
            return false;
        }

        foreach (var node in flows)
        {
            var value = node?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (string.Equals(value, AnonymousMarker, StringComparison.Ordinal)) return true;
            if (string.Equals(value, AuthenticatedMarker, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, flow, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static readonly HashSet<string> HttpVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "trace"
    };

    private static bool IsHttpVerb(string token) => HttpVerbs.Contains(token);
}
