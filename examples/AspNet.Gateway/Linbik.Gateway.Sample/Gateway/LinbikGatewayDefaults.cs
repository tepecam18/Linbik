namespace Linbik.Gateway.Sample.Gateway;

/// <summary>
/// Gateway genelinde kullanılan sabitler: header prefix, PASETO claim → header
/// allowlist, YARP route metadata anahtarları.
/// </summary>
public static class LinbikGatewayDefaults
{
    // ── Header ──────────────────────────────────────────────────────────
    /// <summary>
    /// Downstream'e iletilmeden önce gelen istekten silinecek (ve yeniden
    /// yazılacak) header prefix'i.
    /// </summary>
    public const string HeaderPrefix = "X-Linbik-";

    // ── Meta header'lar ──────────────────────────────────────────────────
    public const string HeaderScheme  = "X-Linbik-Scheme";
    public const string HeaderGateway = "X-Linbik-Gateway";

    // ── YARP route metadata anahtarları ─────────────────────────────────
    public const string MetadataScheme    = "linbik:scheme";
    public const string MetadataAudiences = "linbik:audiences";
    public const string MetadataOpenApiPath = "linbik:openapi-path";

    // ── Scheme etiketleri ────────────────────────────────────────────────
    public const string SchemeLabelSelf      = "self";
    public const string SchemeLabelDelegated = "delegated";
    public const string SchemeLabelApps      = "apps";

    // ── Policy isimleri ─────────────────────────────────────────────────
    public const string PolicySelf      = "SelfPolicy";
    public const string PolicyDelegated = "DelegatedPolicy";
    public const string PolicyApps      = "AppsPolicy";

    /// <summary>
    /// PASETO claim adı → downstream header adı eşlemesi.
    /// Bu allowlist'e dahil olmayan claim'ler downstream'e iletilmez.
    /// Özel eşleme gerekiyorsa appsettings "LinbikGateway:ClaimHeaderMap" ile override edilebilir.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultClaimHeaderMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // User claims (Self + Delegated)
            ["sub"]                  = "X-Linbik-Sub",
            ["name"]                 = "X-Linbik-Name",
            ["preferred_username"]   = "X-Linbik-Username",
            ["azp"]                  = "X-Linbik-Azp",
            ["scope"]                = "X-Linbik-Scope",
            ["role"]                 = "X-Linbik-Roles",
            ["roles"]                = "X-Linbik-Roles",
            // S2S claims (Apps)
            ["source_service_id"]    = "X-Linbik-Source-Service",
            ["source_package"]       = "X-Linbik-Source-Package",
            ["target_package"]       = "X-Linbik-Target-Package",
            // Shared
            ["aud"]                  = "X-Linbik-Audience",
        };
}
