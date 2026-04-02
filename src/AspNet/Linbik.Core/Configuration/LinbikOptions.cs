namespace Linbik.Core.Configuration;

/// <summary>
/// Configuration for a single Linbik client
/// </summary>
public sealed class LinbikClientConfig
{
    /// <summary>
    /// Client name identifier (e.g., "web", "mobile", "admin")
    /// Used for selecting which client config to use in login flow
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Client ID (from Linbik client registration)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URL for this client (e.g., "https://myapp.com")
    /// </summary>
    public string RedirectUrl { get; set; } = "/";

    /// <summary>
    /// Response type for the authorization flow
    /// Redirect: SDK will perform a redirect to the authorization URL (suitable for web apps)
    /// Json: SDK will return the authorization URL and tokens (suitable for mobile apps)
    /// </summary>
    public ActionResultType ActionResultType { get; set; } = ActionResultType.Redirect;
}

/// <summary>
/// Client type enumeration
/// </summary>
public enum ActionResultType
{
    Redirect, // For web apps - SDK will perform a redirect to the authorization URL
    Json      // For mobile apps - SDK will return the authorization URL and tokens
}

public sealed class LinbikOptions
{
    /// <summary>
    /// Linbik server base URL (e.g., "https://linbik.com")
    /// Used for authorization redirects and token exchange
    /// </summary>
    public string LinbikUrl { get; set; } = "https://linbik.com";

    /// <summary>
    /// Service name for identification on Linbik dashboard.
    /// Used by AutoUpdateRedirectUri to match the correct client.
    /// Must match the Name field on Linbik dashboard (case-insensitive).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Enable Keyless Mode for zero-configuration development.
    /// When true, ServiceId/ApiKey/ClientId validation is bypassed.
    /// The SDK will automatically provision a temporary service on first auth request.
    /// Credentials are cached in .linbik/credentials.json.
    /// Default: true
    /// </summary>
    public bool KeylessMode { get; set; } = true;

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
    /// Authorization initiate endpoint path (default: "/api/oauth/initiate")
    /// SDK calls this to start the OAuth flow — saves auth data server-side
    /// and returns a redirect URL with a temporary token.
    /// </summary>
    public string AuthorizationEndpoint { get; set; } = "/api/oauth/initiate";

    /// <summary>
    /// Token exchange endpoint path (default: "/api/oauth/token")
    /// </summary>
    public string TokenEndpoint { get; set; } = "/api/oauth/token";

    /// <summary>
    /// Refresh token endpoint path (default: "/api/oauth/refresh")
    /// </summary>
    public string RefreshEndpoint { get; set; } = "/api/oauth/refresh";

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
    /// S2S token exchange endpoint path (default: "/api/auth/s2s-token")
    /// Used for service-to-service authentication without user context
    /// </summary>
    public string S2STokenEndpoint { get; set; } = "/api/auth/s2s-token";

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

    #region Heartbeat Configuration

    /// <summary>
    /// Enable SDK heartbeat signals to Linbik server.
    /// When enabled, a background service sends periodic heartbeats.
    /// Default: true
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;

    /// <summary>
    /// Heartbeat interval in seconds. Default: 60 (1 minute).
    /// Must be between 10 and 600 (10 minutes).
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 60;

    #endregion
}
