using System.Text.Json.Serialization;

namespace AspNet.Models;

internal sealed record ServerCheckResult(
    string Test,
    bool Passed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Endpoint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Expected = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error = null);

