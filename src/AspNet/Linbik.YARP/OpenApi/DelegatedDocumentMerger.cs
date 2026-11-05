using System.Text;
using System.Text.Json.Nodes;

namespace Linbik.YARP.OpenApi;

/// <summary>Imports delegated operations without overwriting the host's API contract.</summary>
public static class DelegatedDocumentMerger
{
    private static readonly HashSet<string> Methods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    public static JsonObject Merge(JsonObject host, JsonObject source, string packageName, string sourcePath, string targetPath = "")
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(targetPath);
        if (source["openapi"] is not JsonValue version || !version.TryGetValue<string>(out var versionText) ||
            !versionText.StartsWith("3.", StringComparison.Ordinal) || source["paths"] is not JsonObject)
            throw new InvalidOperationException("Expected an OpenAPI 3 document with an 'openapi' version string and a 'paths' object. Check the downstream DocumentPath/DelegatedDocumentPath; it must return OpenAPI JSON rather than an HTML page or API response.");
        if (source["components"] is { } sourceComponents && sourceComponents is not JsonObject)
            throw new InvalidOperationException("Downstream OpenAPI 'components' must be a JSON object.");
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
                    var reference = RequireString(value, key);
                    var match = references.Keys.OrderByDescending(x => x.Length)
                        .FirstOrDefault(x => reference == x || reference.StartsWith(x + "/", StringComparison.Ordinal));
                    if (match is null) throw new InvalidOperationException($"Unsupported OpenAPI reference: {reference}");
                    obj[key] = references[match] + reference[match.Length..];
                }
                else if (key == "operationId" && value is JsonValue) obj[key] = prefix + RequireString(value, key);
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
                        var reference = RequireString(target, "discriminator.mapping." + name);
                        mapping[name] = references.TryGetValue(reference, out var renamed) ? renamed
                            : reference.StartsWith('#') || reference.Contains('/')
                                ? throw new InvalidOperationException($"Unsupported discriminator reference in mapping '{name}'. Use a local component reference or schema name.") : prefix + reference;
                    }
                }
                else Rewrite(value);
            }
        }
        Rewrite(imported);
        var paths = GetOrCreateObject(result, "paths", "host.paths");
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
            var destination = GetOrCreateObject(result, "components", "host.components");
            foreach (var (category, entries) in components)
            {
                if (entries is not JsonObject objects) continue;
                var target = GetOrCreateObject(destination, category, "host.components." + category);
                foreach (var (name, value) in objects)
                {
                    if (target.ContainsKey(prefix + name)) throw new InvalidOperationException($"OpenAPI component collision at components/{category}/{prefix + name}. Use unique integration package names or remove the conflicting host component.");
                    target[prefix + name] = value?.DeepClone();
                }
            }
        }
        return result;
    }

    private static JsonObject GetOrCreateObject(JsonObject owner, string property, string path)
    {
        if (owner[property] is JsonObject existing) return existing;
        if (owner[property] is not null)
            throw new InvalidOperationException($"OpenAPI '{path}' must be a JSON object. Correct the document shape before merging delegated operations.");
        var created = new JsonObject();
        owner[property] = created;
        return created;
    }

    private static string RequireString(JsonNode? node, string path)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text;
        throw new InvalidOperationException($"OpenAPI '{path}' must be a non-null JSON string. Correct the downstream document before merging delegated operations.");
    }

    private static string Escape(string value) => value.Replace("~", "~0").Replace("/", "~1");
}
