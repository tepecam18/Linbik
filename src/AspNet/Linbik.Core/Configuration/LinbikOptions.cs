namespace Linbik.Core.Configuration;

/// <summary>
/// Configuration for a single Linbik client
/// </summary>
public sealed class LinbikClientConfig
{
    /// <summary>
    /// Client ID (from Linbik client registration)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for this client
    /// For web clients: Application base URL (e.g., "https://myapp.com")
    /// For mobile clients: Deep link scheme (e.g., "myapp://")
    /// </summary>
    public string BaseUrl { get; set; } = "/";

    /// <summary>
    /// Default redirect path for this client after successful authentication
    /// Combined with BaseUrl to form complete redirect URL
    /// For web clients: URL path (e.g., "/dashboard")
    /// For mobile clients: Deep link path (e.g., "auth/callback")
    /// </summary>
    public string RedirectUrl { get; set; } = "/";

    /// <summary>
    /// Client type (Web or Mobile)
    /// Web clients receive redirect, Mobile clients receive JSON response
    /// </summary>
    public LinbikClientType ClientType { get; set; } = LinbikClientType.Web;
}

/// <summary>
/// Client type enumeration
/// </summary>
public enum LinbikClientType
{
    /// <summary>
    /// Web client - receives redirects for auth flow
    /// </summary>
    Web,

    /// <summary>
    /// Mobile client - receives JSON responses
    /// </summary>
    Mobile
}

public sealed class LinbikOptions
{
    /// <summary>
    /// Linbik server base URL (e.g., "https://linbik.com")
    /// Used for authorization redirects and token exchange
    /// </summary>
    public string LinbikUrl { get; set; } = "https://linbik.com";

    /// <summary>
    /// application's service ID (from Linbik service registration)
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Multiple clients configuration
    /// Key: client identifier (e.g., "web", "mobile", "admin")
    /// Value: Linbik Client configuration
    /// </summary>
    public List<LinbikClientConfig> Clients { get; set; } = [];

    /// <summary>
    /// Client application's API key (from Linbik service registration)
    /// Used for token exchange and refresh operations
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Authorization endpoint path (default: "auth")
    /// </summary>
    public string AuthorizationEndpoint { get; set; } = "/auth";

    /// <summary>
    /// Token exchange endpoint path (default: "oauth/token")
    /// </summary>
    public string TokenEndpoint { get; set; } = "/auth/token";

    /// <summary>
    /// Refresh token endpoint path (default: "oauth/refresh")
    /// </summary>
    public string RefreshEndpoint { get; set; } = "/auth/refresh";

    /// <summary>
    /// Authorization code lifetime in minutes (default: 10)
    /// </summary>
    public int AuthorizationCodeLifetimeMinutes { get; set; } = 10;

    /// <summary>
    /// Access token (JWT) lifetime in minutes (default: 60)
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token lifetime in days (default: 30)
    /// </summary>
    public int RefreshTokenLifetimeDays { get; set; } = 30;

    /// <summary>
    /// Enable PKCE (Proof Key for Code Exchange) validation
    /// Recommended for public clients (mobile apps, SPAs)
    /// </summary>
    public bool EnablePKCE { get; set; } = true;

    /// <summary>
    /// JWT issuer name (default: "Linbik")
    /// </summary>
    public string JwtIssuer { get; set; } = LinbikDefaults.Issuer;

    #region S2S (Service-to-Service) Configuration

    /// <summary>
    /// S2S token exchange endpoint path (default: "auth/s2s-token")
    /// Used for service-to-service authentication without user context
    /// </summary>
    public string S2STokenEndpoint { get; set; } = "/auth/s2s-token";

    /// <summary>
    /// S2S access token (JWT) lifetime in minutes (default: 60)
    /// S2S tokens typically have the same lifetime as user tokens
    /// </summary>
    public int S2STokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Target services for S2S authentication
    /// Key: Package name (e.g., "payment-gateway")
    /// Value: Service ID (GUID from Linbik registration)
    /// Used by GetS2STokensAsync(packageNames) method
    /// </summary>
    public Dictionary<string, Guid> S2STargetServices { get; set; } = [];

    /// <summary>
    /// Enable automatic S2S token refresh before expiration
    /// When true, tokens are refreshed automatically when 75% of lifetime has passed
    /// </summary>
    public bool S2SAutoRefresh { get; set; } = true;

    /// <summary>
    /// S2S token refresh threshold as percentage of token lifetime
    /// Token will be refreshed when this percentage of lifetime has passed
    /// Default: 0.75 (75% - refresh at 45 mins for 60 min token)
    /// </summary>
    public double S2SRefreshThreshold { get; set; } = 0.75;

    #endregion
}
