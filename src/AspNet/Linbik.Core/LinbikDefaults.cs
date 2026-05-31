namespace Linbik.Core;

/// <summary>
/// Central constants shared across all Linbik packages.
/// Prevents magic string duplication and ensures consistency.
/// </summary>
public static class LinbikDefaults
{
    // ─── Token Issuer ─────────────────────────────────────────────────────
    /// <summary>
    /// Default issuer name used across all Linbik services
    /// </summary>
    public const string Issuer = "Linbik";

    // ─── Authentication Schemes ───────────────────────────────────
    /// <summary>
    /// Client-side HS256 JWT scheme (cookie-based, for main services).
    /// Use with [LinbikAuthorize] attribute
    /// </summary>
    public const string ClientScheme = "LinbikScheme";

    /// <summary>
    /// Server-side PASETO v4.public scheme for user-initiated (delegated) requests.
    /// Use with [LinbikDelegatedAuthorize] attribute
    /// </summary>
    public const string DelegatedScheme = "LinbikDelegated";

    /// <summary>
    /// Server-side PASETO v4.public scheme for service-to-service requests.
    /// Use with [LinbikApplicationAuthorize] attribute
    /// </summary>
    public const string ApplicationScheme = "LinbikApplication";

    // ─── Authorization Policies ───────────────────────────────────
    /// <summary>
    /// YARP proxy authorization policy (RequireAuthenticatedUser)
    /// </summary>
    public const string ProxyPolicy = "LinbikProxyPolicy";

    // ─── Cookie Names ─────────────────────────────────────────────
    /// <summary>
    /// Cookie name for the local JWT access token
    /// </summary>
    public const string AuthTokenCookie = "authToken";

    /// <summary>
    /// Cookie name for the refresh token
    /// </summary>
    public const string RefreshTokenCookie = "linbikRefreshToken";

    /// <summary>
    /// Cookie prefix for per-integration-service tokens (e.g., "integration_payment")
    /// </summary>
    public const string IntegrationTokenPrefix = "integration_";

    /// <summary>
    /// Cookie name for storing the return URL during login flow
    /// </summary>
    public const string ReturnUrlCookie = "linbik_return_url";

    /// <summary>
    /// Cookie name for storing the user's display name
    /// </summary>
    public const string UserNameCookie = "userName";

    // ─── Diagnostic Headers ───────────────────────────────────────
    /// <summary>
    /// SDK operation mode (Keyless, Standard, CLI)
    /// </summary>
    public const string HeaderMode = "Linbik-Mode";

    /// <summary>
    /// SDK version (e.g., "1.2.0")
    /// </summary>
    public const string HeaderVersion = "Linbik-Version";

    /// <summary>
    /// SDK platform (e.g., "aspnet", "nuxt")
    /// </summary>
    public const string HeaderPlatform = "Linbik-Platform";

    /// <summary>
    /// Client type (e.g., "Web", "Mobile")
    /// </summary>
    public const string HeaderClientType = "Linbik-Client";

    // ─── Gateway → Service Contract ───────────────────────────────
    /// <summary>
    /// API Gateway tarafından downstream servise iletilen yetkilendirme akışı.
    /// Değerleri için bkz. <see cref="Flows"/>.
    /// </summary>
    public const string HeaderFlow = "Linbik-Flow";

    /// <summary>
    /// API Gateway → Service header'larında kullanılan yetkilendirme akış adları.
    /// Bu sabitler hem <c>[LFlowAuthorize(...)]</c> deklarasyonlarında hem de
    /// YARP route metadata'sında ortak kullanılır.
    /// </summary>
    public static class Flows
    {
        /// <summary>Self / cookie tabanlı kullanıcı oturumu (ClientScheme).</summary>
        public const string Self = "Self";

        /// <summary>Başka bir uygulama, son kullanıcı adına çağırıyor (DelegatedScheme).</summary>
        public const string Delegated = "Delegated";

        /// <summary>Service-to-service / client_credentials (ApplicationScheme).</summary>
        public const string Application = "Application";

        /// <summary>
        /// Bilinçli olarak public/anonim endpoint. Deny-by-default analyzer'ı
        /// tatmin etmek için açıkça işaretlenir; gateway doc'larında her flow'a görünür.
        /// </summary>
        public const string Public = "*";
    }
}
