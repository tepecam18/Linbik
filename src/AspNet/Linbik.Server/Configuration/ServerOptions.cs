namespace Linbik.Server.Configuration;

public class ServerOptions
{
    /// <summary>
    /// Service ID (GUID) - this integration service's unique identifier
    /// Used for validating that incoming tokens are intended for this service
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// RSA public key for JWT validation (PEM format or Base64 DER)
    /// This is the public key corresponding to this service's private key
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Authorization code lifetime in minutes (default: 10)
    /// </summary>
    public int AuthorizationCodeLifetimeMinutes { get; set; } = 10;

    /// <summary>
    /// Access token (JWT) lifetime in minutes (default: 60)
    /// </summary>
    public int AccessTokenExpiration { get; set; } = 60;

    /// <summary>
    /// Refresh token lifetime in days (default: 30)
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>
    /// JWT issuer name (default: "Linbik")
    /// </summary>
    public string JwtIssuer { get; set; } = "Linbik";

    /// <summary>
    /// Enable PKCE (Proof Key for Code Exchange) validation
    /// </summary>
    public bool EnablePKCE { get; set; } = true;

    /// <summary>
    /// Enable IP whitelisting validation for services
    /// </summary>
    public bool EnableIpWhitelisting { get; set; } = true;

    /// <summary>
    /// Clock skew tolerance for token validation (default: 5 minutes)
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;

    /// <summary>
    /// Require ServiceId validation in token audience
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Require issuer validation
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;
}
