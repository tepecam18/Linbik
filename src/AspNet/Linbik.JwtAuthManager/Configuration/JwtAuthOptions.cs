namespace Linbik.JwtAuthManager.Configuration;

/// <summary>
/// JWT authentication configuration options
/// Used for local JWT token generation and validation
/// </summary>
public class JwtAuthOptions
{
    /// <summary>
    /// Secret key for symmetric JWT signing (HS256/HS512)
    /// For production, use RSA keys with RS256 instead
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// JWT issuer name (default: "linbik")
    /// </summary>
    public string JwtIssuer { get; set; } = "linbik";

    /// <summary>
    /// JWT audience (default: "linbik-client")
    /// </summary>
    public string JwtAudience { get; set; } = "linbik-client";

    /// <summary>
    /// Access token (JWT) lifetime in minutes (default: 60)
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token lifetime in days (default: 14)
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 14;

    /// <summary>
    /// Enable PKCE (Proof Key for Code Exchange) validation
    /// Recommended for public clients (mobile apps, SPAs)
    /// </summary>
    public bool PkceEnabled { get; set; } = true;

    /// <summary>
    /// Login path (redirects to Linbik authorization)
    /// </summary>
    public string LoginPath { get; set; } = "/linbik/login";

    /// <summary>
    /// Login callback path (receives authorization code)
    /// </summary>
    public string LoginCallbackPath { get; set; } = "/linbik/callback";

    /// <summary>
    /// Logout path
    /// </summary>
    public string LogoutPath { get; set; } = "/linbik/logout";

    /// <summary>
    /// Token refresh path
    /// </summary>
    public string RefreshPath { get; set; } = "/linbik/refresh";

    /// <summary>
    /// Login redirect URL (after successful authentication)
    /// </summary>
    public string LoginRedirectUrl { get; set; } = "/";

    /// <summary>
    /// Error redirect URL (when authentication fails)
    /// Use {error} placeholder for error message
    /// Example: "/error?message={error}"
    /// </summary>
    public string ErrorRedirectUrl { get; set; } = "/error?message={error}";

    /// <summary>
    /// Logout redirect URL (after successful logout)
    /// </summary>
    public string LogoutRedirectUrl { get; set; } = "/";
}
