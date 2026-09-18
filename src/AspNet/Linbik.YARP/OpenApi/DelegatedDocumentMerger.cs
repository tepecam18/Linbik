using System.Text;
using System.Text.Json.Nodes;

namespace Linbik.YARP.OpenApi;

/// <summary>Imports delegated operations without overwriting the host's API contract.</summary>
public static class DelegatedDocumentMerger
{
    private static readonly HashSet<string> Methods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    public static JsonObject Merge(JsonObject host, JsonObject source, string packageName, string sourcePath, string targetPath = "")
    {
        if (source["openapi"]?.GetValue<string>().StartsWith("3.", StringComparison.Ordinal) != true || source["paths"] is not JsonObject)
            throw new InvalidOperationException("Expected an OpenAPI 3 document with paths.");
        var result = (JsonObject)host.DeepClone();
        var imported = (JsonObject)source.DeepClone();
        var prefix = "linbik_" + Convert.ToHexString(Encoding.UTF8.GetBytes(packageName)) + "_";
        var pathPrefix = "/" + sourcePath.Trim('/');
        var targetPrefix = string.IsNullOrEmpty(targetPath.Trim('/')) ? "" : "/" + targetPath.Trim('/');
        string PublicPath(string path)
        {
            if (targetPrefix.Length == 0) return pathPrefix + path;
            if (path.Equals(targetPrefix, StringComparison.Ordinal)) return pathPrefix;
            if (path.StartsWith(targetPrefix + "/", StringComparison.Ordinal))
                return pathPrefix + path[targetPrefix.Length..];
            throw new InvalidOperationException($"OpenAPI path '{path}' is outside TargetPath '{targetPrefix}'.");
        }
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        if (imported["components"] is JsonObject originalComponents)
            foreach (var (category, entries) in originalComponents)
                if (entries is JsonObject objects)
                    foreach (var (name, _) in objects)
                        references[$"#/components/{Escape(category)}/{Escape(name)}"] = $"#/components/{Escape(category)}/{Escape(prefix + name)}";
        foreach (var (path, _) in (JsonObject)imported["paths"]!)
            references["#/paths/" + Escape(path)] = "#/paths/" + Escape(PublicPath(path));

        void Rewrite(JsonNode? node)
        {
            if (node is JsonArray array) { foreach (var child in array) Rewrite(child); return; }
            if (node is not JsonObject obj) return;
            foreach (var (key, value) in obj.ToList())
            {
                if (key is "$ref" or "operationRef" && value is JsonValue)
                {
                    var reference = value.GetValue<string>();
                    var match = references.Keys.OrderByDescending(x => x.Length)
                        .FirstOrDefault(x => reference == x || reference.StartsWith(x + "/", StringComparison.Ordinal));
                    if (match is null) throw new InvalidOperationException($"Unsupported OpenAPI reference: {reference}");
                    obj[key] = references[match] + reference[match.Length..];
                }
                else if (key == "operationId" && value is JsonValue) obj[key] = prefix + value.GetValue<string>();
                else if (key == "security" && value is JsonArray security)
                {
                    foreach (var requirement in security.OfType<JsonObject>())
                        foreach (var (name, scopes) in requirement.ToList())
                        { requirement.Remove(name); requirement[prefix + name] = scopes; }
                }
                else if (key == "mapping" && value is JsonObject mapping && obj.ContainsKey("propertyName"))
                {
                    foreach (var (name, target) in mapping.ToList())
                    {
                        var reference = target!.GetValue<string>();
                        mapping[name] = references.TryGetValue(reference, out var renamed) ? renamed
                            : reference.StartsWith('#') || reference.Contains('/')
                                ? throw new InvalidOperationException("Unsupported discriminator reference.") : prefix + reference;
                    }
                }
                else Rewrite(value);
            }
        }
        Rewrite(imported);
        result["paths"] ??= new JsonObject();
        var paths = (JsonObject)result["paths"]!;
        foreach (var (path, item) in (JsonObject)imported["paths"]!)
        {
            if (!path.StartsWith('/') || item is not JsonObject pathItem)
                throw new InvalidOperationException("Invalid OpenAPI path.");
            var publicPath = PublicPath(path);
            if (paths.ContainsKey(publicPath)) throw new InvalidOperationException($"OpenAPI path collision: {publicPath}");
            // Override remote servers AND host root servers so Try It uses the local proxy.
            pathItem["servers"] = new JsonArray(new JsonObject { ["url"] = "/" });
            foreach (var (method, operation) in pathItem)
                if (Methods.Contains(method) && operation is JsonObject op)
                {
                    op.Remove("servers");
                    if (!op.ContainsKey("security"))
                        op["security"] = imported["security"]?.DeepClone() ?? new JsonArray();
                }
            paths[publicPath] = pathItem.DeepClone();
        }
        if (imported["components"] is JsonObject components)
        {
            result["components"] ??= new JsonObject();
            var destination = (JsonObject)result["components"]!;
            foreach (var (category, entries) in components)
            {
                if (entries is not JsonObject objects) continue;
                destination[category] ??= new JsonObject();
                var target = (JsonObject)destination[category]!;
                foreach (var (name, value) in objects)
                {
                    if (target.ContainsKey(prefix + name)) throw new InvalidOperationException("OpenAPI component collision.");
                    target[prefix + name] = value?.DeepClone();
                }
            }
        }
        return result;
    }

    private static string Escape(string value) => value.Replace("~", "~0").Replace("/", "~1");
}
