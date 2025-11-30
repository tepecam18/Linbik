namespace Linbik.Core.Configuration;

public class LinbikOptions
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
    /// Client application's API key (from Linbik service registration)
    /// Used for token exchange and refresh operations
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Authorization endpoint path (default: "/auth")
    /// </summary>
    public string AuthorizationEndpoint { get; set; } = "/auth";

    /// <summary>
    /// Token exchange endpoint path (default: "/oauth/token")
    /// </summary>
    public string TokenEndpoint { get; set; } = "/auth/token";

    /// <summary>
    /// Refresh token endpoint path (default: "/oauth/refresh")
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
    /// JWT issuer name (default: "linbik")
    /// </summary>
    public string JwtIssuer { get; set; } = "Linbik";

    /// <summary>
    /// Legacy: Allowed app IDs (deprecated - use service registration instead)
    /// </summary>
    [Obsolete("Use service registration with API keys instead")]
    public string[] AppIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Legacy: Public key (deprecated - use per-service keys instead)
    /// </summary>
    [Obsolete("Use per-service RSA key pairs instead")]
    public string PublicKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Legacy: If true, all apps are allowed (deprecated)
    /// </summary>
    [Obsolete("Use proper service registration and API key validation")]
    public bool AllowAllApp { get; set; } = false;
}
