using Microsoft.IdentityModel.Tokens;

namespace Linbik.JwtAuthManager.Configuration;

public class JwtAuthOptions
{
    /// <summary>
    /// Legacy: Private key (deprecated - use per-service RSA keys)
    /// </summary>
    [Obsolete("Use per-service RSA key pairs instead")]
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Legacy: Algorithm (deprecated - use RS256)
    /// </summary>
    [Obsolete("Use RS256 (RSA-SHA256) with per-service keys")]
    public string Algorithm { get; set; } = SecurityAlgorithms.HmacSha512Signature;

    /// <summary>
    /// Legacy: Login path (deprecated - use OAuth 2.0 flow)
    /// </summary>
    [Obsolete("Use OAuth 2.0 authorization flow instead")]
    public string LoginPath { get; set; } = "/linbik/login";

    /// <summary>
    /// Legacy: Refresh login path (deprecated)
    /// </summary>
    [Obsolete("Use /oauth/refresh endpoint instead")]
    public string RefreshLoginPath { get; set; } = "/linbik/refresh-token";

    /// <summary>
    /// Legacy: Exit path (deprecated)
    /// </summary>
    [Obsolete("Use proper logout endpoint")]
    public string ExitPath { get; set; } = "/linbik/logout";

    /// <summary>
    /// Legacy: PKCE start path (deprecated)
    /// </summary>
    [Obsolete("PKCE is now integrated into /auth/{serviceId}/{code_challenge} endpoint")]
    public string PkceStartPath { get; set; } = "/linbik/pkce-start";

    /// <summary>
    /// Enable PKCE (Proof Key for Code Exchange) validation
    /// Recommended for public clients (mobile apps, SPAs)
    /// </summary>
    public bool PkceEnabled { get; set; } = true;

    /// <summary>
    /// Access token (JWT) lifetime in minutes (default: 60)
    /// </summary>
    public int AccessTokenExpiration { get; set; } = 60;
    
    /// <summary>
    /// Refresh token lifetime in days (default: 30)
    /// </summary>
    public int RefreshTokenExpiration { get; set; } = 30;

    /// <summary>
    /// JWT issuer name (default: "linbik")
    /// </summary>
    public string JwtIssuer { get; set; } = "linbik";

    /// <summary>
    /// Legacy: Custom routes (deprecated)
    /// </summary>
    [Obsolete("Use standard OAuth 2.0 endpoints")]
    public Dictionary<string, string> Routes { get; set; } = new();

    /// <summary>
    /// Legacy: Referer control (deprecated)
    /// </summary>
    [Obsolete("Use proper CORS and origin validation")]
    public bool RefererControl { get; set; } = false;
}
