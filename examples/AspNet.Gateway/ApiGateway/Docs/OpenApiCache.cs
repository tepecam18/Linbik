using System.Text.Json.Nodes;

namespace ApiGateway.Docs;

/// <summary>
/// Bir downstream servis için son başarılı OpenAPI çekişinin sonucu.
/// <para>
/// <see cref="EtagSetAt"/>, 200 OK ile bu ETag'in alındığı andır. 304 cycle'larında
/// güncellenmez. <see cref="LinbikGatewayOptions.EtagMaxAgeSeconds"/> aşıldığında
/// aggregator <c>If-None-Match</c> göndermeden tam fetch yapar — kalıcı ETag
/// poisoning veya bayat cache durumlarına karşı güvenlik valfi.
/// </para>
/// </summary>
public sealed record DownstreamOpenApiSnapshot(
    string ServicePrefix,
    string? DisplayName,
    string? ETag,
    DateTimeOffset EtagSetAt,
    string RawJson,
    JsonNode Document,
    DateTimeOffset FetchedAt,
    bool IsGateway = false);

/// <summary>
/// Aggregator'ın in-memory cache'i. Tek BackgroundService writer + lock'lu reader.
/// </summary>
public sealed class OpenApiCache
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DownstreamOpenApiSnapshot> _byPrefix = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FilteredDocumentCache> _filtered = new(StringComparer.Ordinal);

    public bool HasAny
    {
        get { lock (_gate) { return _byPrefix.Count > 0; } }
    }

    public IReadOnlyList<DownstreamOpenApiSnapshot> SnapshotAll()
    {
        lock (_gate) { return _byPrefix.Values.ToArray(); }
    }

    public DownstreamOpenApiSnapshot? TryGet(string servicePrefix)
    {
        lock (_gate)
        {
            return _byPrefix.TryGetValue(servicePrefix, out var s) ? s : null;
        }
    }

    public void Upsert(DownstreamOpenApiSnapshot snapshot)
    {
        lock (_gate)
        {
            _byPrefix[snapshot.ServicePrefix] = snapshot;
            _filtered.Clear();
        }
    }

    public bool TryGetFiltered(string flowKey, out string json)
    {
        lock (_gate)
        {
            if (_filtered.TryGetValue(flowKey, out var cached))
            {
                json = cached.Json;
                return true;
            }
        }
        json = string.Empty;
        return false;
    }

    public void SetFiltered(string flowKey, string json)
    {
        lock (_gate)
        {
            _filtered[flowKey] = new FilteredDocumentCache(json, DateTimeOffset.UtcNow);
        }
    }

    private sealed record FilteredDocumentCache(string Json, DateTimeOffset BuiltAt);
}
