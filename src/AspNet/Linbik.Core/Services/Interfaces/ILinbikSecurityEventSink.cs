namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// Linbik SDK üzerinde gerçekleşen güvenlik olaylarını (authentication / authorization / API key
/// validation hataları) tüketici uygulamaya iletmek için kullanılan hafif soyutlama.
/// 
/// <para>
/// Default <see cref="NoOpSecurityEventSink"/> implementation'ı kayıtlıdır ve hiçbir şey yapmaz.
/// Tüketici uygulama (ör. <c>Linbik.Api</c>) kendi adapter'ını <see cref="IServiceEventLogger"/>
/// veya benzeri bir log altyapısına köprü kurmak için kayıt edebilir:
/// <code>
/// services.AddScoped&lt;ILinbikSecurityEventSink, MyAdapter&gt;();
/// </code>
/// </para>
/// 
/// <para>Olay kategorileri için <see cref="LinbikSecurityEventType"/>'a bakın.</para>
/// </summary>
public interface ILinbikSecurityEventSink
{
    /// <summary>
    /// Bir güvenlik olayını tüketicinin log/analitik altyapısına gönderir.
    /// Implementasyon hatayı asla yukarıya fırlatmamalıdır — sink kayıt edilirken tüm hatalar
    /// silently swallow edilmelidir, aksi halde kimlik doğrulama akışını bozar.
    /// </summary>
    Task ReportAsync(LinbikSecurityEvent securityEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// SDK katmanında gerçekleşen güvenlik olaylarının kategorisi. Tüketici tarafta 
/// <c>service_event_logs.event_type</c> alanına bire bir map'lenir.
/// </summary>
public static class LinbikSecurityEventType
{
    /// <summary>JWT (kullanıcı veya apps) imza/exp/issuer doğrulaması başarısız.</summary>
    public const string AuthenticationFailed = "authentication_failed";

    /// <summary>Kimlik doğrulanmış ama policy/role yetki vermedi (HTTP 403).</summary>
    public const string AuthorizationFailed = "authorization_failed";

    /// <summary>API anahtarı yanlış / iptal edilmiş / süresi dolmuş.</summary>
    public const string ApiKeyInvalid = "api_key_invalid";

    /// <summary>Apps token, beklenenin aksine user-token (cross-scheme injection denemesi).</summary>
    public const string ApplicationJwtInvalid = "application_jwt_invalid";

    /// <summary>Rate limit aşıldı.</summary>
    public const string RateLimitExceeded = "rate_limit_exceeded";
}

/// <summary>
/// Bir güvenlik olayının taşıdığı veri. Tüketici uygulama bunu kendi
/// log şemasına çevirir (ör. <c>service_event_logs</c> tablosu).
/// </summary>
public sealed class LinbikSecurityEvent
{
    /// <summary>Olay türü, bkz. <see cref="LinbikSecurityEventType"/> sabitleri.</summary>
    public required string EventType { get; init; }

    /// <summary>İnsan-okunaklı kısa açıklama. Hassas bilgi içermemelidir.</summary>
    public required string Message { get; init; }

    /// <summary>HTTP status code (ör. 401, 403, 429). Bilinmiyorsa null.</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>Çağrılan endpoint path'i (örn. <c>/api/orders/123</c>).</summary>
    public string? RequestPath { get; init; }

    /// <summary>HTTP method (GET, POST, ...).</summary>
    public string? RequestMethod { get; init; }

    /// <summary>İstemcinin IP adresi (varsa).</summary>
    public string? RemoteIp { get; init; }

    /// <summary>User-Agent header değeri.</summary>
    public string? UserAgent { get; init; }

    /// <summary>JWT'den çözümlenen kullanıcı id'si (ör. <c>sub</c> claim). Olay anonim ise null.</summary>
    public string? ActorUserId { get; init; }

    /// <summary>Olayın ait olduğu hedef servis id'si (token içindeki <c>aud</c> veya <c>target_service_id</c>).</summary>
    public Guid? TargetServiceId { get; init; }

    /// <summary>Source service id'si (Application call'larda token içindeki <c>source_service_id</c>).</summary>
    public Guid? SourceServiceId { get; init; }

    /// <summary>Ek metadata (ör. failed_claim_name, expected_role, attempted_role). JSON serialize edilebilir.</summary>
    public object? Metadata { get; init; }
}

/// <summary>
/// SDK içinde varsayılan olarak kayıt edilen no-op sink. Kayıt yapmaz; tüketici
/// uygulama kendi implementasyonunu çağırarak override edebilir.
/// </summary>
public sealed class NoOpSecurityEventSink : ILinbikSecurityEventSink
{
    public Task ReportAsync(LinbikSecurityEvent securityEvent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
