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
    /// Bu tag'i taşıyan operasyonlar (örn. AppsService'in Linbik platform lifecycle
    /// endpoint'leri) gerçek erişimde <c>linbik-flows</c>'daki akışı (örn. Application)
    /// kullanmaya devam eder — downstream <c>[LFlowAuthorize]</c> değişmez. Ama bu, o
    /// akışın **public dokümanında** görünmesi gerektiği anlamına gelmez: bu tag'e sahip
    /// operasyonlar yalnız <see cref="FlowSelf"/> dokümanında listelenir, diğer akış
    /// dokümanlarından (Delegated/Application) her zaman gizlenir. <c>LinbikGatewaySource.IsGateway</c>'in
    /// source-level karşılığı — burada operation-level, herhangi bir servis kullanabilir.
    /// </summary>
    public const string SystemTagName = "Linbik System";

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
            FlowApplication => "Linbik Gateway — Application (application bearer)",
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

            if (downstream["paths"] is not JsonObject downstreamPaths) continue;

            // Bu snapshot'tan flow filtresinden geçen path item'lar; şemalar bunlardan erişilebilirliğe göre seçilir.
            var snapshotItems = new List<(string Path, JsonObject Item)>();

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

                    // Operasyonun kendi tag'lerini (servis tarafında WithTags ile atanan,
                    // örn. "Reactions", "Comments") global tags listesine unique olarak topla.
                    // NOT: Servis adını (DisplayName) operasyona AYRICA eklemiyoruz — aksi
                    // halde her operasyon hem kendi feature grubunda hem de servis-genel
                    // grubunda (ör. "Comment") tekrar görünür (duplicate grouping).
                    if (clonedOp["tags"] is JsonArray opTags)
                    {
                        foreach (var t in opTags)
                        {
                            var tv = t?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(tv) &&
                                !tags.Any(gt => string.Equals(gt?["name"]?.GetValue<string>(), tv, StringComparison.Ordinal)))
                            {
                                tags.Add(new JsonObject { ["name"] = tv });
                            }
                        }
                    }

                    rewrittenPathItem[verb] = clonedOp;
                }

                // Hiç verb kalmadıysa path'i ekleme.
                if (rewrittenPathItem.Count(kv => IsHttpVerb(kv.Key)) == 0)
                    continue;

                snapshotItems.Add((publicPath, rewrittenPathItem));
            }

            if (snapshotItems.Count == 0) continue;

            // Yalnız kalan operasyonlardan erişilebilen şemaları al; çakışmada {ServicePrefix}_{Name}.
            if (downstream["components"] is JsonObject comps && comps["schemas"] is JsonObject schemas)
            {
                var reachable = ReachableSchemas(snapshotItems.Select(i => i.Item), schemas);
                var renames = new Dictionary<string, string>(StringComparer.Ordinal);
                var toAdd = new List<(string Name, JsonNode Node)>();

                foreach (var name in reachable)
                {
                    if (schemas[name] is not { } schemaNode) continue;

                    if (componentsSchemas[name] is { } existingSchema)
                    {
                        if (JsonNode.DeepEquals(existingSchema, schemaNode)) continue;
                        var newName = snap.ServicePrefix + "_" + name;
                        renames[name] = newName;
                        toAdd.Add((newName, schemaNode.DeepClone()));
                    }
                    else
                    {
                        toAdd.Add((name, schemaNode.DeepClone()));
                    }
                }

                if (renames.Count > 0)
                {
                    foreach (var (_, item) in snapshotItems) RewriteSchemaRefs(item, renames);
                    foreach (var (_, node) in toAdd) RewriteSchemaRefs(node, renames);
                }

                foreach (var (name, node) in toAdd)
                    componentsSchemas[name] = node;
            }

            foreach (var (publicPath, item) in snapshotItems)
            {
                // Aynı public path birden çok source'tan gelebilir (teorik olarak): merge et.
                if (paths[publicPath] is JsonObject existing)
                {
                    foreach (var (k, v) in item)
                    {
                        existing[k] = v?.DeepClone();
                    }
                }
                else
                {
                    paths[publicPath] = item;
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

    private const string SchemaRefPrefix = "#/components/schemas/";

    private static void CollectSchemaRefs(JsonNode? node, HashSet<string> found)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value is JsonValue jv && jv.TryGetValue<string>(out var r) &&
                        r.StartsWith(SchemaRefPrefix, StringComparison.Ordinal))
                        found.Add(r[SchemaRefPrefix.Length..]);
                    else
                        CollectSchemaRefs(value, found);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) CollectSchemaRefs(item, found);
                break;
        }
    }

    private static HashSet<string> ReachableSchemas(IEnumerable<JsonNode> roots, JsonObject allSchemas)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        var direct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots) CollectSchemaRefs(root, direct);
        foreach (var name in direct) queue.Enqueue(name);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!seen.Add(name)) continue;
            if (allSchemas[name] is not { } schema) continue;

            var next = new HashSet<string>(StringComparer.Ordinal);
            CollectSchemaRefs(schema, next);
            foreach (var n in next) queue.Enqueue(n);
        }
        return seen;
    }

    private static void RewriteSchemaRefs(JsonNode? node, IReadOnlyDictionary<string, string> renames)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (key == "$ref" && value is JsonValue jv && jv.TryGetValue<string>(out var r) &&
                        r.StartsWith(SchemaRefPrefix, StringComparison.Ordinal) &&
                        renames.TryGetValue(r[SchemaRefPrefix.Length..], out var renamed))
                        obj[key] = SchemaRefPrefix + renamed;
                    else
                        RewriteSchemaRefs(value, renames);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) RewriteSchemaRefs(item, renames);
                break;
        }
    }

    private static string ResolvePublicPrefix(string flow) => flow switch
    {
        FlowSelf => "/api",                  // /api/{servicePrefix}/...
        FlowDelegated => "/api/delegated",
        FlowApplication => "/api/apps",
        _ => "/api"
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
        // Sistem tag'i: gerçek erişim linbik-flows'a göre çalışmaya devam eder (aşağıda),
        // ama public dokümanda yalnız Self'te gösterilir — bkz. SystemTagName.
        if (HasSystemTag(op) && !string.Equals(flow, FlowSelf, StringComparison.OrdinalIgnoreCase))
            return false;

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

    private static bool HasSystemTag(JsonObject op)
    {
        if (op["tags"] is not JsonArray tags) return false;
        foreach (var t in tags)
        {
            if (string.Equals(t?.GetValue<string>(), SystemTagName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static readonly HashSet<string> HttpVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "trace"
    };

    private static bool IsHttpVerb(string token) => HttpVerbs.Contains(token);
}
