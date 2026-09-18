using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Linbik.YARP.OpenApi;

internal static class DelegatedDocumentReader
{
    public static OpenApiDocument Read(JsonObject json)
    {
        // Keep Linbik metadata outside the strict parser, then attach it unchanged.
        var normalized = json.DeepClone();
        var flows = new Dictionary<string, JsonArray>();
        Extract(normalized, "", flows);
        var parsed = OpenApiDocument.Parse(normalized.ToJsonString(), "json");
        if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0)
        {
            var details = parsed.Diagnostic?.Errors.Select(error => $"{error.Pointer}: {error.Message}");
            throw new InvalidOperationException("Invalid OpenAPI document: " +
                (details is null ? "Parser returned no document." : string.Join("; ", details)));
        }
        new OpenApiWalker(new RestoreFlows(flows)).Walk(parsed.Document);
        return parsed.Document;
    }

    private sealed class RestoreFlows(Dictionary<string, JsonArray> flows) : OpenApiVisitorBase
    {
        public override void Visit(OpenApiOperation operation)
        {
            if (flows.TryGetValue(PathString.TrimStart('#'), out var value))
            {
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["linbik-flows"] = new JsonNodeExtension(value.DeepClone());
            }
        }
    }

    private static void Extract(JsonNode? node, string path, Dictionary<string, JsonArray> metadata)
    {
        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++) Extract(array[i], path + "/" + i, metadata);
        }
        else if (node is JsonObject obj)
        {
            if (obj["linbik-flows"] is JsonArray flows)
            {
                metadata[path] = (JsonArray)flows.DeepClone();
                obj.Remove("linbik-flows");
            }
            foreach (var (key, child) in obj)
            {
                // These contain user-defined keys/data, not OpenAPI operation metadata.
                if (key is "schema" or "schemas" or "example" or "examples" or "default" or "enum") continue;
                Extract(child, path + "/" + key.Replace("~", "~0").Replace("/", "~1"), metadata);
            }
        }
    }
}
