using System.Text.Json.Nodes;

namespace Linbik.YARP.Services;

/// <summary>
/// Rewrites OpenAPI documents produced by Microsoft.AspNetCore.OpenApi (.NET 9+) into a shape NSwag
/// generates correct C# for. ASP.NET Core emits numeric properties as
/// <c>"type": ["integer", "string"]</c> with a numeric <c>pattern</c> (it accepts quoted numbers).
/// NSwag reads that as a flags enum, picks <c>int</c> for the property type, but quotes the default
/// value because <c>String</c> is set, producing uncompilable code such as
/// <c>public int Count { get; set; } = "1";</c>.
/// </summary>
internal static class NSwagDocumentNormalizer
{
    public static string Normalize(string json)
    {
        var root = JsonNode.Parse(json);
        if (root is null)
            return json;

        Walk(root);
        return root.ToJsonString();
    }

    private static void Walk(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                CollapseNumericStringUnion(obj);
                foreach (var child in obj.Select(p => p.Value).ToList())
                {
                    if (child is not null)
                        Walk(child);
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (child is not null)
                        Walk(child);
                }
                break;
        }
    }

    private static void CollapseNumericStringUnion(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
            return;

        var names = types.Select(t => t?.GetValue<string>()).ToList();
        if (!names.Contains("string") || !(names.Contains("integer") || names.Contains("number")))
            return;

        var remaining = new JsonArray();
        foreach (var name in names.Where(n => n is not null && n != "string"))
            remaining.Add(name);

        schema["type"] = remaining.Count == 1 ? JsonValue.Create(remaining[0]!.GetValue<string>()) : remaining;
        schema.Remove("pattern");
    }
}
