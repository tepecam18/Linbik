namespace Linbik.Server.Models;

/// <summary>
/// Token type enumeration for different authentication scenarios
/// </summary>
public enum LinbikTokenType
{
    /// <summary>
    /// Token issued for user-initiated service requests
    /// Contains: UserId, Username, DisplayName, AuthorizedParty
    /// </summary>
    UserService,

    /// <summary>
    /// Token issued for Service-to-Service requests (no user context)
    /// Contains: SourceServiceId, SourcePackageName
    /// </summary>
    S2S
}

/// <summary>
/// Validated token claims from JWT
/// </summary>
public sealed class LinbikTokenClaims
{
    /// <summary>
    /// Token type: UserService or S2S
    /// </summary>
    public LinbikTokenType TokenType { get; set; } = LinbikTokenType.UserService;

    /// <summary>
    /// User ID (sub claim) - Only present in UserService tokens
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Username - Only present in UserService tokens
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Display name / Nickname - Only present in UserService tokens
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Package Name (aud claim) - the integration service this token is for
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Authorized party (azp claim) - the main service that requested this token
    /// Present in both UserService and S2S tokens
    /// </summary>
    public Guid AuthorizedParty { get; set; }

    /// <summary>
    /// Source Service ID (sub claim for S2S tokens) - Only present in S2S tokens
    /// </summary>
    public Guid? SourceServiceId { get; set; }

    /// <summary>
    /// Source Package Name - Only present in S2S tokens
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
    /// Check if this is a user-service token
    /// </summary>
    public bool IsUserServiceToken => TokenType == LinbikTokenType.UserService;

    /// <summary>
    /// Check if this is an S2S token
    /// </summary>
    public bool IsS2SToken => TokenType == LinbikTokenType.S2S;
}
