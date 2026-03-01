namespace Linbik.JwtAuthManager.Configuration;

/// <summary>
/// JWT authentication configuration options
/// Used for local JWT token generation and validation
/// </summary>
public sealed class JwtAuthOptions
{
    /// <summary>
    /// Secret key for symmetric JWT signing (HS256/HS512)
    /// For production, use RSA keys with RS256 instead
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// JWT issuer name (default: "Linbik")
    /// </summary>
    public string JwtIssuer { get; set; } = Core.LinbikDefaults.Issuer;

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
    public string LoginPath { get; set; } = "/api/linbik/login";

    /// <summary>
    /// Login callback path (receives authorization code)
    /// </summary>
    public string LoginCallbackPath { get; set; } = "/api/linbik/callback";

    /// <summary>
    /// Logout path
    /// </summary>
    public string LogoutPath { get; set; } = "/api/linbik/logout";

    /// <summary>
    /// Token refresh path
    /// </summary>
    public string RefreshPath { get; set; } = "/api/linbik/refresh";
}
