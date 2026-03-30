namespace Linbik.Core;

/// <summary>
/// Central constants shared across all Linbik packages.
/// Prevents magic string duplication and ensures consistency.
/// </summary>
public static class LinbikDefaults
{
    // ─── JWT Issuer ───────────────────────────────────────────────
    /// <summary>
    /// Default JWT issuer name used across all Linbik services
    /// </summary>
    public const string Issuer = "Linbik";

    // ─── Authentication Schemes ───────────────────────────────────
    /// <summary>
    /// Client-side HS256 JWT scheme (cookie-based, for main services)
    /// Use with [LinbikAuthorize] attribute
    /// </summary>
    public const string ClientScheme = "LinbikScheme";

    /// <summary>
    /// Server-side RS256 JWT scheme for user-initiated requests
    /// Use with [LinbikUserServiceAuthorize] attribute
    /// </summary>
    public const string UserServiceScheme = "LinbikUserService";

    /// <summary>
    /// Server-side RS256 JWT scheme for service-to-service requests
    /// Use with [LinbikS2SAuthorize] attribute
    /// </summary>
    public const string S2SScheme = "LinbikS2S";

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
    public const string HeaderMode = "X-Linbik-Mode";

    /// <summary>
    /// SDK version (e.g., "1.2.0")
    /// </summary>
    public const string HeaderVersion = "X-Linbik-Version";

    /// <summary>
    /// SDK platform (e.g., "aspnet", "nuxt")
    /// </summary>
    public const string HeaderPlatform = "X-Linbik-Platform";

    /// <summary>
    /// Client type (e.g., "Web", "Mobile")
    /// </summary>
    public const string HeaderClientType = "X-Linbik-Client";
}
