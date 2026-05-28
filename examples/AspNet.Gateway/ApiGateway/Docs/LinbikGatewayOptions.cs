namespace ApiGateway.Docs;

/// <summary>
/// Gateway konfigürasyonu (appsettings.json -&gt; <c>LinbikGateway</c> section'ı).
/// Hem YARP route/cluster auto-generation hem OpenAPI aggregator için tek kaynak.
/// </summary>
public sealed class LinbikGatewayOptions
{
    public const string SectionName = "LinbikGateway";

    /// <summary>BackgroundService poll periyodu. 0 ya da negatif değer = poll yok (yalnız lazy).</summary>
    public int RefreshIntervalSeconds { get; set; } = 60;

    /// <summary>Downstream OpenAPI fetch için HttpClient zaman aşımı.</summary>
    public int FetchTimeoutSeconds { get; set; } = 5;

    /// <summary>Downstream servisinde OpenAPI JSON URL path'i (örn. <c>/openapi/v1.json</c>).</summary>
    public string DownstreamOpenApiPath { get; set; } = "/openapi/v1.json";

    /// <summary>
    /// ETag'in zorla yenileneceği maksimum yaş. Bu süre dolduktan sonraki refresh cycle
    /// <c>If-None-Match</c> header'ı eklemeden tam fetch yapar; cache poisoning ve
    /// kalıcı stale durumlarına karşı güvenlik tedbiridir.
    /// </summary>
    public int EtagMaxAgeSeconds { get; set; } = 600;

    public List<LinbikGatewaySource> Sources { get; set; } = new();

    /// <summary>Doc endpoint auth davranışı (self/delegated/apps JSON + UI sayfaları).</summary>
    public LinbikDocsAuthOptions Docs { get; set; } = new();
}

/// <summary>
/// Doc erişim auth modeli. Varsayılan:
/// <list type="bullet">
/// <item><b>Development</b>: tüm doc endpoint'leri (JSON + UI) anonim erişilebilir.</item>
/// <item><b>Production</b>: <c>self.json</c> → <c>LinbikAuthorize</c> (cookie);
/// <c>delegated.json</c> → Delegated veya Application bearer;
/// <c>apps.json</c> → Application bearer; <c>/docs/*</c> UI sayfaları cookie ile.</item>
/// </list>
/// </summary>
public sealed class LinbikDocsAuthOptions
{
    /// <summary>
    /// <c>true</c> ise Development ortamında da doc endpoint'leri auth ister
    /// (Prod davranışı). Varsayılan <c>false</c> — Dev'de geliştirici deneyimi
    /// için tüm doc'lar anonim erişilebilir.
    /// </summary>
    public bool RequireAuthInDevelopment { get; set; } = false;
}

/// <summary>
/// Tek bir downstream servis kaynağı — YARP'ta otomatik olarak 1 cluster ve 3 route
/// (self / delegated / apps) üretir, aynı zamanda OpenAPI aggregator için fetch hedefidir.
/// </summary>
public sealed class LinbikGatewaySource
{
    /// <summary>
    /// Servis adı (örn. <c>arithmetic</c>). Üretilen path'lerde ve cluster id'sinde kullanılır.
    /// Gateway public path: <c>/{ServicePrefix}/…</c> (self), <c>/delegated/{ServicePrefix}/…</c>,
    /// <c>/apps/{ServicePrefix}/…</c>. Downstream transform: <c>/api/{ServicePrefix}/…</c>.
    /// </summary>
    public string ServicePrefix { get; set; } = string.Empty;

    /// <summary>Downstream servis base address'i (örn. <c>http://localhost:5181/</c>).</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Dokümantasyon tag/grup adı. Boşsa <see cref="ServicePrefix"/> başharfli yazılır.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// <c>true</c> ise bu kaynak gateway'in <b>kendisidir</b>: <see cref="LinbikRouteBuilder"/>
    /// route üretmez (gateway zaten kendi controller'larına serves eder) ve
    /// <see cref="FilteredDocumentBuilder"/> path rewrite uygulamaz — yani
    /// <c>/api/linbik/login</c> gibi gateway-yerel path'ler self doc'a olduğu gibi düşer.
    /// PasetoAuthManager endpoint'lerini (<c>/api/linbik/*</c>) doc'a dahil etmek için kullanılır.
    /// </summary>
    public bool IsGateway { get; set; }
}
