namespace Linbik.Server.Models;

/// <summary>
/// Token type enumeration for different authentication scenarios
/// </summary>
public enum LinbikTokenType
{
    /// <summary>
    /// Delegated token issued for user-initiated service requests
    /// Contains: UserId, Username, DisplayName, AuthorizedParty
    /// </summary>
    Delegated,

    /// <summary>
    /// Application token issued for service-to-service requests (no user context)
    /// Contains: SourceServiceId, SourcePackageName
    /// </summary>
    Application
}

/// <summary>
/// Validated token claims from JWT
/// </summary>
public sealed class LinbikTokenClaims
{
    /// <summary>
    /// Token type: Delegated or Application
    /// </summary>
    public LinbikTokenType TokenType { get; set; } = LinbikTokenType.Delegated;

    /// <summary>
    /// User ID (sub claim) - Only present in delegated tokens
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Username - Only present in delegated tokens
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Display name / Nickname - Only present in delegated tokens
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Package Name (aud claim) - the integration service this token is for
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Authorized party (azp claim) - the main service that requested this token
    /// Present in both Delegated and Application tokens
    /// </summary>
    public Guid AuthorizedParty { get; set; }

    /// <summary>
    /// Source Service ID (sub claim for application tokens) - Only present in application tokens
    /// </summary>
    public Guid? SourceServiceId { get; set; }

    /// <summary>
    /// Source Package Name - Only present in application tokens
    /// </summary>
    public string? SourcePackageName { get; set; }

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
    public Dictionary<string, string> RawClaims { get; set; } = [];

    /// <summary>
    /// Check if this is a delegated token
    /// </summary>
    public bool IsDelegatedToken => TokenType == LinbikTokenType.Delegated;

    /// <summary>
    /// Check if this is an application token
    /// </summary>
    public bool IsApplicationToken => TokenType == LinbikTokenType.Application;
}
