namespace Linbik.PasetoAuthManager.Configuration;

/// <summary>
/// PASETO v4 authentication configuration options.
/// Supports both v4.public (Ed25519 asymmetric signing) and v4.local
/// (XChaCha20-Poly1305 symmetric encryption) modes.
/// </summary>
public sealed class PasetoAuthOptions
{
    /// <summary>
    /// PASETO mode to use. Defaults to <see cref="Core.Services.Interfaces.PasetoMode.Local"/>
    /// for backwards compatibility. Use <see cref="Core.Services.Interfaces.PasetoMode.Public"/>
    /// for asymmetric scenarios where a public/private key pair is required.
    /// </summary>
    public Core.Services.Interfaces.PasetoMode Mode { get; set; } = Core.Services.Interfaces.PasetoMode.Local;

    /// <summary>
    /// (v4.public only) Ed25519 private key for PASETO signing (Base64-encoded, 64 bytes: seed||public).
    /// Generate with <c>IPasetoHelper.GenerateKeyPair()</c>.
    /// </summary>
    public string PrivateKeyBase64 { get; set; } = string.Empty;

    /// <summary>
    /// (v4.public only) Ed25519 public key for PASETO validation (Base64-encoded, 32 bytes).
    /// </summary>
    public string PublicKeyBase64 { get; set; } = string.Empty;

    /// <summary>
    /// (v4.local only) Symmetric shared key for PASETO encryption/decryption
    /// (Base64-encoded, 32 bytes). Generate with <c>IPasetoHelper.GenerateSymmetricKey()</c>.
    /// </summary>
    public string SharedKeyBase64 { get; set; } = string.Empty;

    /// <summary>
    /// PASETO issuer name (default: "Linbik").
    /// </summary>
    public string Issuer { get; set; } = Core.LinbikDefaults.Issuer;

    /// <summary>
    /// PASETO audience (default: "linbik-client").
    /// </summary>
    public string Audience { get; set; } = "linbik-client";

    /// <summary>
    /// Optional key identifier embedded in the PASETO footer for key rotation support.
    /// </summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// Access token (PASETO) lifetime in minutes (default: 60).
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token lifetime in days (default: 14).
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 14;

    /// <summary>
    /// Enable PKCE (Proof Key for Code Exchange) validation.
    /// Recommended for public clients (mobile apps, SPAs).
    /// </summary>
    public bool PkceEnabled { get; set; } = true;

    /// <summary>
    /// Login path (redirects to Linbik authorization).
    /// </summary>
    public string LoginPath { get; set; } = "/api/Linbik/login";

    /// <summary>
    /// Login callback path (receives authorization code).
    /// </summary>
    public string LoginCallbackPath { get; set; } = "/api/Linbik/callback";

    /// <summary>
    /// When true, EnsureLinbik() will automatically update the RedirectUri
    /// on Linbik server using LinbikOptions.Name to find the matching client.
    /// </summary>
    public bool AutoUpdateRedirectUri { get; set; } = false;

    /// <summary>
    /// Logout path.
    /// </summary>
    public string LogoutPath { get; set; } = "/api/Linbik/logout";

    /// <summary>
    /// Token refresh path.
    /// </summary>
    public string RefreshPath { get; set; } = "/api/Linbik/refresh";

    /// <summary>
    /// Cookie domain for the username cookie (e.g. ".example.com" for cross-subdomain sharing).
    /// Defaults to null — uses the current request host.
    /// </summary>
    public string? CookieDomain { get; set; }
}
