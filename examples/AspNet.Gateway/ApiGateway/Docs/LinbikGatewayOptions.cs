using System.Security.Claims;

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
/// <item><b>Development</b>: <c>delegated.json</c>/<c>apps.json</c> (+ ilgili UI'ler)
/// anonim erişilebilir. <c>self.json</c>/<c>/docs/self</c> <see cref="SelfAccess"/>
/// tarafından yönetilir (bu örnekte <c>appsettings.Development.json</c> "anonim" olarak override eder).</item>
/// <item><b>Production</b>: <c>delegated.json</c> → Delegated veya Application bearer;
/// <c>apps.json</c> → Application bearer; <c>/docs/*</c> UI sayfaları cookie ile.
/// <c>self.json</c>/<c>/docs/self</c> yine <see cref="SelfAccess"/> tarafından yönetilir
/// (varsayılan: kapalı).</item>
/// </list>
/// </summary>
public sealed class LinbikDocsAuthOptions
{
    /// <summary>
    /// <c>true</c> ise Development ortamında da <b>delegated</b>/<b>apps</b> doc
    /// endpoint'leri auth ister (Prod davranışı). Varsayılan <c>false</c> — Dev'de
    /// geliştirici deneyimi için bu doc'lar anonim erişilebilir. <c>self.json</c>/
    /// <c>/docs/self</c> bu flag'den etkilenmez, bkz. <see cref="SelfAccess"/>.
    /// </summary>
    public bool RequireAuthInDevelopment { get; set; } = false;

    /// <summary>
    /// <c>self.json</c> / <c>/docs/self</c> için erişim kuralı. Desteklenen değerler:
    /// <list type="bullet">
    /// <item>boş / <c>null</c> (varsayılan) — <b>kapalı</b>, kimse erişemez.</item>
    /// <item><c>"anonim"</c> — herkese açık, oturum gerekmez.</item>
    /// <item><c>"*"</c> — oturum açmış (cookie ile authenticate olmuş) herhangi bir kullanıcı.</item>
    /// <item>virgülle ayrılmış kullanıcı adı listesi (örn. <c>"ali,veli,mehmet"</c>) —
    /// yalnızca bu kullanıcı adlarına sahip oturumlar erişebilir.</item>
    /// </list>
    /// Kullanıcı adı karşılaştırması <see cref="IsSelfAccessAllowed"/> tarafından yapılır.
    /// </summary>
    public string? SelfAccess { get; set; }

    /// <summary>
    /// <see cref="SelfAccess"/> kuralını verilen <paramref name="user"/> için değerlendirir.
    /// Karşılaştırmalar <see cref="StringComparison.OrdinalIgnoreCase"/> ile yapılır
    /// (Türkçe "I/İ" culture sorunlarından kaçınmak için).
    /// </summary>
    public static bool IsSelfAccessAllowed(string? rule, ClaimsPrincipal user)
    {
        rule = rule?.Trim();
        if (string.IsNullOrEmpty(rule))
            return false; // kapalı

        if (rule.Equals("anonim", StringComparison.OrdinalIgnoreCase))
            return true;

        if (user.Identity is not { IsAuthenticated: true })
            return false;

        if (rule == "*")
            return true;

        var userName = user.FindFirst("preferred_username")?.Value
            ?? user.Identity.Name
            ?? user.FindFirst("sub")?.Value;

        if (userName is null)
            return false;

        return rule
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allowed => string.Equals(allowed, userName, StringComparison.OrdinalIgnoreCase));
    }
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
