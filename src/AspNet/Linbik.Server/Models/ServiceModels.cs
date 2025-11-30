namespace Linbik.Server.Models;

/// <summary>
/// Validated token claims from JWT
/// </summary>
public class LinbikTokenClaims
{
    /// <summary>
    /// User ID (sub claim)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Display name / Nickname
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Service ID (aud claim) - the integration service this token is for
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Authorized party (azp claim) - the main service that requested this token
    /// </summary>
    public Guid AuthorizedParty { get; set; }

    /// <summary>
    /// Token issue time
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// Token expiration time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Token issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// All raw claims from the token
    /// </summary>
    public Dictionary<string, string> RawClaims { get; set; } = new();
}
